module Tags

open System
open IO
open FSharp.Data
open Errors
open Operators
open Utilities
open FsToolkit.ErrorHandling

type NewTags =
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

type CheckResult =
    | Unchanged
    | Updated
    | New

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

type CachedTagProvider = JsonProvider<tagSample>
type CachedTagRoot = CachedTagProvider.Root
type FileNameWithCachedTags = Map<string, CachedTagRoot>
type CheckResultWithFileTags = CheckResult * NewTags

let parseCachedTagData json : Result<CachedTagRoot array, Error> =
    try
        json
        |> CachedTagProvider.Parse
        |> Ok
    with
    | e -> Error (ParseError e.Message)

let audioFilePath (tags: CachedTagRoot) : string =
    Path.Combine [| tags.DirectoryName; tags.FileNameOnly |]

let createCachedTagMap (cachedTagFile: FileInfo) : Result<FileNameWithCachedTags, Error> =
    if cachedTagFile.Exists
    then
        cachedTagFile.FullName
        |> readfile
        >>= parseCachedTagData
        <!> Array.map (fun tags -> audioFilePath tags, tags)
        <!> Map.ofArray
    else
        Ok Map.empty

let compareAndUpdateTagData
    (cachedTags: FileNameWithCachedTags)
    (fileInfos: FileInfo seq)
    : CheckResultWithFileTags seq
    =
    let createNewTagData (fileInfo: FileInfo) =
        let currentTags = readFileTags fileInfo.FullName

        if currentTags.Tag = null
        then
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
                LastWriteTime = fileInfo.LastWriteTime |> DateTimeOffset
            }
        else
            {
                FileNameOnly = fileInfo.Name
                DirectoryName = fileInfo.DirectoryName
                Artists = if currentTags.Tag.Performers = null
                          then [| String.Empty |]
                          else currentTags.Tag.Performers
                               |> Array.map (fun p -> p.Normalize())
                AlbumArtists = if currentTags.Tag.AlbumArtists = null
                               then [| String.Empty |]
                               else currentTags.Tag.AlbumArtists |> Array.map (fun p -> p.Normalize())
                Album = if currentTags.Tag.Album = null
                        then String.Empty
                        else currentTags.Tag.Album.Normalize()
                TrackNo = currentTags.Tag.Track
                Title = currentTags.Tag.Title
                Year = currentTags.Tag.Year
                Genres = currentTags.Tag.Genres
                Duration = currentTags.Properties.Duration
                LastWriteTime = fileInfo.LastWriteTime |> DateTimeOffset
            }

    let useExistingTagData (cachedTags: CachedTagRoot) =
        {
            FileNameOnly = cachedTags.FileNameOnly
            DirectoryName = cachedTags.DirectoryName
            Artists = cachedTags.Artists
            AlbumArtists = cachedTags.AlbumArtists
            Album = cachedTags.Album
            TrackNo = uint cachedTags.TrackNo
            Title = cachedTags.Title
            Year = uint cachedTags.Year
            Genres = cachedTags.Genres
            Duration = cachedTags.Duration
            LastWriteTime = DateTimeOffset cachedTags.LastWriteTime.DateTime
        }

    let updateTags (cachedTags: FileNameWithCachedTags) (fileInfo: FileInfo) : CheckResultWithFileTags =
        if Map.containsKey fileInfo.FullName cachedTags
        then
            let fileCachedTags = Map.find fileInfo.FullName cachedTags
            if fileCachedTags.LastWriteTime.DateTime < fileInfo.LastWriteTime
            then Updated, (createNewTagData fileInfo)
            else Unchanged, (useExistingTagData fileCachedTags)
        else New, (createNewTagData fileInfo)

    fileInfos
    |> Seq.map (fun fileInfo -> updateTags cachedTags fileInfo)

let reportResults (results: CheckResultWithFileTags seq) : CheckResultWithFileTags seq =
    let initialCounts = {| Added = 0; Updated = 0; Unchanged = 0 |}

    let totals =
        (initialCounts, results |> Seq.map fst)
        ||> Seq.fold (fun acc result ->
            match result with
            | New -> {| acc with Added = acc.Added + 1 |}
            | Updated -> {| acc with Updated = acc.Updated + 1 |}
            | Unchanged -> {| acc with Unchanged = acc.Unchanged + 1 |})

    printfn "Results:"
    printfn "• New:       %s" (formatNumber totals.Added)
    printfn "• Updated:   %s" (formatNumber totals.Updated)
    printfn "• Unchanged: %s" (formatNumber totals.Unchanged)
    printfn "• Total:     %s" (formatNumber (Seq.length results))

    results

let generateNewJson
    (cachedTagMap: FileNameWithCachedTags)
    (fileInfos: FileInfo seq)
    : Result<string, Error>
    =
    fileInfos
    |> compareAndUpdateTagData cachedTagMap
    |> reportResults
    |> Seq.map snd
    |> serializeToJson
    |> Result.mapError JsonSerializationError
