module Tags

open System
open IO
open FSharp.Data
open Errors
open Operators
open Utilities
open FsToolkit.ErrorHandling

type FileTagsToWrite =
    {
        FileNameOnly: string
        DirectoryName: string
        Artists: string array
        AlbumArtists: string array
        Album: string
        TrackNo: uint
        Title: string
        Year: uint
        Genres: string array
        Duration: TimeSpan
        LastWriteTime: DateTimeOffset
    }

type ComparisonResult =
    | Unchanged
    | OutOfDate // The file tags are newer than the library's.
    | NotPresent // Tags for the specified file don't exist in the tag library.

[<Literal>]
let private tagSample = """
[
  {
    "FileNameOnly": "name",
    "DirectoryName": "name",
    "Artists": ["name"],
    "AlbumArtists": ["name"],
    "Album": "name",
    "TrackNo": 0,
    "Title": "name",
    "Year": 0,
    "Genres": ["name"],
    "Duration": "00:00:00",
    "LastWriteTime": "2023-09-13T13:49:44+09:00"
  }
]"""

type TagLibraryProvider = JsonProvider<tagSample>
type TagLibraryMap = Map<string, TagLibraryProvider.Root>
type ComparisonResultWithNewTags = ComparisonResult * FileTagsToWrite

let parseTagLibrary json : Result<TagLibraryProvider.Root array, Error> =
    try
        json
        |> TagLibraryProvider.Parse
        |> Ok
    with
    | e -> Error (ParseError e.Message)

let audioFilePath (fileTags: TagLibraryProvider.Root) : string =
    Path.Combine [| fileTags.DirectoryName; fileTags.FileNameOnly |]

let createTagLibraryMap (tagLibraryFile: FileInfo) : Result<TagLibraryMap, Error> =
    if tagLibraryFile.Exists
    then
        tagLibraryFile.FullName
        |> readfile
        >>= parseTagLibrary
        <!> Array.map (fun tags -> audioFilePath tags, tags)
        <!> Map.ofArray
    else
        Ok Map.empty

let compareAndUpdateTagData (tagLibraryMap: TagLibraryMap) (fileInfos: FileInfo seq)
    : ComparisonResultWithNewTags seq
    =
    let copyFromLibrary (libraryTags: TagLibraryProvider.Root) =
        {
            FileNameOnly = libraryTags.FileNameOnly
            DirectoryName = libraryTags.DirectoryName
            Artists = libraryTags.Artists
            AlbumArtists = libraryTags.AlbumArtists
            Album = libraryTags.Album
            TrackNo = uint libraryTags.TrackNo
            Title = libraryTags.Title
            Year = uint libraryTags.Year
            Genres = libraryTags.Genres
            Duration = libraryTags.Duration
            LastWriteTime = DateTimeOffset libraryTags.LastWriteTime.DateTime
        }

    let createNew (fileInfo: FileInfo) : FileTagsToWrite =
        let blankTags =
            {
                FileNameOnly = fileInfo.Name
                DirectoryName = fileInfo.DirectoryName
                Artists = [| String.Empty |]
                AlbumArtists = [| String.Empty |]
                Album = String.Empty
                TrackNo = 0u
                Title = String.Empty
                Year = 0u
                Genres = [| String.Empty |]
                Duration = TimeSpan.Zero
                LastWriteTime = DateTimeOffset fileInfo.LastWriteTime
            }

        let readFromFile (fileInfo: FileInfo) (fileTags: TaggedFile) =
            {
                FileNameOnly = fileInfo.Name
                DirectoryName = fileInfo.DirectoryName
                Artists = fileTags.Tag.Performers |> Array.map _.Normalize()
                AlbumArtists = fileTags.Tag.AlbumArtists |> Array.map _.Normalize()
                Album = fileTags.Tag.Album.Normalize()
                TrackNo = fileTags.Tag.Track
                Title = fileTags.Tag.Title
                Year = fileTags.Tag.Year
                Genres = fileTags.Tag.Genres
                Duration = fileTags.Properties.Duration
                LastWriteTime = DateTimeOffset fileInfo.LastWriteTime
            }

        let fileTags = parseFileTags fileInfo.FullName

        match fileTags with
        | Error _ -> blankTags
        | Ok fileTags ->
            if fileTags.Tag = null
            then blankTags
            else readFromFile fileInfo fileTags

    let updateTags (tagLibraryMap: TagLibraryMap) (audioFile: FileInfo) : ComparisonResultWithNewTags =
        if Map.containsKey audioFile.FullName tagLibraryMap
        then
            let libraryTags = Map.find audioFile.FullName tagLibraryMap
            if libraryTags.LastWriteTime.DateTime < audioFile.LastWriteTime
            then OutOfDate, (createNew audioFile)
            else Unchanged, (copyFromLibrary libraryTags)
        else NotPresent, (createNew audioFile)

    fileInfos
    |> Seq.map (updateTags tagLibraryMap)

let reportResults (results: ComparisonResultWithNewTags seq) : ComparisonResultWithNewTags seq =
    let initialCounts = {| NotPresent = 0; OutOfDate = 0; Unchanged = 0 |}

    let totals =
        (initialCounts, results |> Seq.map fst)
        ||> Seq.fold (fun acc result ->
            match result with
            | NotPresent -> {| acc with NotPresent = acc.NotPresent + 1 |}
            | OutOfDate -> {| acc with OutOfDate = acc.OutOfDate + 1 |}
            | Unchanged -> {| acc with Unchanged = acc.Unchanged + 1 |})

    printfn "Results:"
    printfn "• New:       %s" (formatNumber totals.NotPresent)
    printfn "• Updated:   %s" (formatNumber totals.OutOfDate)
    printfn "• Unchanged: %s" (formatNumber totals.Unchanged)
    printfn "• Total:     %s" (formatNumber (Seq.length results))

    results

let generateNewJson
    (tagLibraryMap: TagLibraryMap)
    (fileInfos: FileInfo seq)
    : Result<string, Error>
    =
    fileInfos
    |> compareAndUpdateTagData tagLibraryMap
    |> reportResults
    |> Seq.map snd
    |> serializeToJson
    |> Result.mapError JsonSerializationError
