module AudioTagTools.Cacher

open System
open System.Globalization
open FSharp.Data
open FsToolkit.ErrorHandling
open System.IO
open Shared

type TaggedFile = TagLib.File

module Errors =
    type Error =
        | InvalidArgCount
        | MediaDirectoryMissing of string
        | ReadFileError of string
        | WriteFileError of string
        | IoError of string
        | ParseError of string
        | JsonSerializationError of string

    let message = function
        | InvalidArgCount -> "Invalid arguments. Pass in (1) the directory containing your audio files and (2) a path to a JSON file containing cached tag data."
        | MediaDirectoryMissing msg -> $"Directory \"{msg}\" was not found."
        | ReadFileError msg -> $"Read failure: {msg}"
        | WriteFileError msg -> $"Write failure: {msg}"
        | IoError msg -> $"I/O failure: {msg}"
        | ParseError msg -> $"Parse error: {msg}"
        | JsonSerializationError msg -> $"JSON serialization error: {msg}"

module ArgValidation =
    open Errors

    let validate (args: string array) : Result<DirectoryInfo * FileInfo, Error> =
        if args.Length = 3 // Index 0 is the name of the script itself.
        then Ok (DirectoryInfo args[1], FileInfo args[2])
        else Error InvalidArgCount

module Utilities =
    open Errors
    open System.Text.Json
    open System.Text.Encodings.Web
    open System.Text.Unicode

    let formatWithCommas (i: int) =
        i.ToString("N0", CultureInfo.InvariantCulture)

    let serializeToJson items =
        try
            let serializerOptions = JsonSerializerOptions()
            serializerOptions.WriteIndented <- true
            serializerOptions.Encoder <- JavaScriptEncoder.Create UnicodeRanges.All
            JsonSerializer.Serialize(items, serializerOptions)
            |> Ok
        with
        | e -> Error (JsonSerializationError e.Message)

module IO =
    open Errors

    let readFile (fileName: string) : Result<string, Error> =
        try
            fileName
            |> System.IO.File.ReadAllText
            |> Ok
        with
        | e -> Error (ReadFileError e.Message)

    let getFileInfos (dirPath: DirectoryInfo) =
        let isSupportedAudioFile (fileInfo: FileInfo) =
            [".mp3"; ".m4a"; ".mp4"; ".ogg"; ".flac"]
            |> List.contains fileInfo.Extension

        try
            dirPath.EnumerateFiles("*", SearchOption.AllDirectories)
            |> Seq.filter isSupportedAudioFile
            |> Ok
        with
        | e -> Error (IoError e.Message)

    let readFileTags (filePath: string) : TaggedFile =
        TagLib.File.Create filePath

    let writeFile (fileName: string) (content: string) =
        try
            File.WriteAllText(fileName, content)
            |> Ok
        with
        | e -> Error (WriteFileError e.Message)

    let generateBackUpFilePath (cachedTagFile: FileInfo) =
        let baseName = Path.GetFileNameWithoutExtension cachedTagFile.Name
        let timestamp = DateTimeOffset.Now.ToString "yyyyMMdd_HHmmss"
        let extension = cachedTagFile.Extension // Includes the initial period.
        let fileName = sprintf "%s-%s%s" baseName timestamp extension
        Path.Combine(cachedTagFile.DirectoryName, fileName)

    let copyToBackupFile (cachedTagFile: FileInfo) =
        let printConfirmation (backupFile: FileInfo) =
            printfn "Backed up previous tag file to \"%s\"." backupFile.Name
            backupFile

        if cachedTagFile.Exists
        then
            try
                cachedTagFile
                |> generateBackUpFilePath
                |> cachedTagFile.CopyTo
                |> printConfirmation
                |> Some
                |> Ok
            with
            | e -> Error (IoError $"Could not create tag backup file: {e.Message}")
        else Ok None

module Tags =
    open IO
    open Operators
    open Utilities
    open Errors

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

    let createCachedTagMap (cachedTagFile: FileInfo) : Result<Map<string, CachedTagRoot>, Error> =
        if cachedTagFile.Exists
        then
            cachedTagFile.FullName
            |> readFile
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

    let reportResults (results: CheckResultWithFileTags seq) =
        let initialCounts = {| Added = 0; Updated = 0; Unchanged = 0 |}

        let totals =
            (initialCounts, results |> Seq.map fst)
            ||> Seq.fold (fun acc result ->
                match result with
                | New -> {| acc with Added = acc.Added + 1 |}
                | Updated -> {| acc with Updated = acc.Updated + 1 |}
                | Unchanged -> {| acc with Unchanged = acc.Unchanged + 1 |})

        printfn "Results:"
        printfn "• New:       %s" (formatWithCommas totals.Added)
        printfn "• Updated:   %s" (formatWithCommas totals.Updated)
        printfn "• Unchanged: %s" (formatWithCommas totals.Unchanged)
        printfn "• Total:     %s" (formatWithCommas (Seq.length results))

        results

    let generateNewJson (cachedTagMap: FileNameWithCachedTags) (fileInfos: FileInfo seq) =
        fileInfos
        |> compareAndUpdateTagData cachedTagMap
        |> reportResults
        |> Seq.map snd
        |> serializeToJson

open Errors
open IO
open Tags

let run (args: string array) =
    result {
        let! mediaDir, tagCacheFile = ArgValidation.validate args
        let! fileInfos = getFileInfos mediaDir
        let! cachedTagMap = createCachedTagMap tagCacheFile
        let! newJson = generateNewJson cachedTagMap fileInfos

        let! _ = copyToBackupFile tagCacheFile
        do! writeFile tagCacheFile.FullName newJson
    }

let start args =
    match run args with
    | Ok _ -> Ok "Finished caching successfully!"
    | Error e -> Error (message e)
