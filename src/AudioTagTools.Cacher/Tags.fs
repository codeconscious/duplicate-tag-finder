module Tags

open System
open System.Text.Json
open IO
open Errors
open Operators
open Utilities
open FsToolkit.ErrorHandling

type LibraryTags =
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

type TagMap = Map<string, LibraryTags>

type LibraryComparisonResult =
    | Unchanged // Library tags match file tags.
    | OutOfDate // Library tags are older than file tags.
    | NotPresent // No tags exist in library for file.

type CategorizedTagsToCache =
    { Type: LibraryComparisonResult
      Tags: LibraryTags }

let createTagLibraryMap (libraryFile: FileInfo) : Result<TagMap, Error> =
    let parseTagLibrary (json: string) : Result<LibraryTags array, Error> =
        try Ok (JsonSerializer.Deserialize<LibraryTags array>(json))
        with e -> Error (ParseError e.Message)

    let audioFilePath (fileTags: LibraryTags) : string =
        Path.Combine [| fileTags.DirectoryName; fileTags.FileNameOnly |]

    if libraryFile.Exists
    then
        libraryFile.FullName
        |> readfile
        >>= parseTagLibrary
        <!> Array.map (fun tags -> audioFilePath tags, tags)
        <!> Map.ofArray
    else
        Ok Map.empty

let private prepareTagsToWrite (tagLibraryMap: TagMap) (fileInfos: FileInfo seq)
    : CategorizedTagsToCache seq
    =
    let copyCachedTags (libraryTags: LibraryTags) =
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

    let generateTags (fileInfo: FileInfo) : LibraryTags =
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

        let tagsFromFile (fileInfo: FileInfo) (fileTags: TaggedFile) =
            {
                FileNameOnly = fileInfo.Name
                DirectoryName = fileInfo.DirectoryName
                Artists = if fileTags.Tag.Performers = null
                          then [| String.Empty |]
                          else fileTags.Tag.Performers
                               |> Array.map (fun p -> p.Normalize())
                AlbumArtists = if fileTags.Tag.AlbumArtists = null
                               then [| String.Empty |]
                               else fileTags.Tag.AlbumArtists |> Array.map _.Normalize()
                Album = if fileTags.Tag.Album = null
                        then String.Empty
                        else fileTags.Tag.Album.Normalize()
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
            else tagsFromFile fileInfo fileTags

    let prepareTagsToCache (tagLibraryMap: TagMap) (audioFile: FileInfo) : CategorizedTagsToCache =
        if Map.containsKey audioFile.FullName tagLibraryMap
        then
            let libraryTags = Map.find audioFile.FullName tagLibraryMap
            if libraryTags.LastWriteTime.DateTime < audioFile.LastWriteTime
            then { Type = OutOfDate; Tags = (generateTags audioFile) }
            else { Type = Unchanged; Tags = (copyCachedTags libraryTags) }
        else { Type = NotPresent; Tags = (generateTags audioFile) }

    fileInfos
    |> Seq.map (prepareTagsToCache tagLibraryMap)

let private reportResults (results: CategorizedTagsToCache seq) : CategorizedTagsToCache seq =
    let initialCounts = {| NotPresent = 0; OutOfDate = 0; Unchanged = 0 |}

    let totals =
        (initialCounts, Seq.map _.Type results)
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
    (tagLibraryMap: TagMap)
    (fileInfos: FileInfo seq)
    : Result<string, Error>
    =
    fileInfos
    |> prepareTagsToWrite tagLibraryMap
    |> reportResults
    |> Seq.map _.Tags
    |> serializeToJson
    |> Result.mapError JsonSerializationError
