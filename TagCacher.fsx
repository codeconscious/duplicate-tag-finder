#r "nuget: TagLibSharp"
#r "nuget: FsToolkit.ErrorHandling"
#r "nuget: FSharp.Data"
#r "nuget: CodeConscious.Startwatch, 1.0.0"

open System
open System.Globalization
open FSharp.Data
open FsToolkit.ErrorHandling
open System.IO

type TaggedFile = TagLib.File

module Errors =
    type Errors =
        | InvalidArgCount
        | MediaDirectoryMissing of string
        | IoError of string

    let message = function
        | InvalidArgCount -> "Invalid arguments. Pass in (1) the directory containing audio files and (2) a path to a JSON file containing cached tag data."
        | MediaDirectoryMissing e -> $"The directory \"{e}\" was not found."
        | IoError e -> $"I/O failure: {e}"

module ArgValidation =
    open Errors

    let validate =
        if fsi.CommandLineArgs.Length <> 3 // Index 0 is the name of the script itself.
        then Error InvalidArgCount
        else
            let mediaDir, cachedTagFile = DirectoryInfo fsi.CommandLineArgs[1], FileInfo fsi.CommandLineArgs[2]

            if not mediaDir.Exists
            then Error (MediaDirectoryMissing mediaDir.FullName)
            else Ok (mediaDir, cachedTagFile)

module Utilities =
    open System.Text.Json
    open System.Text.Encodings.Web
    open System.Text.Unicode

    let extractText (x: Runtime.BaseTypes.IJsonDocument) =
        x.JsonValue.InnerText()

    let joinWithSeparator (separator: string) (xs: Runtime.BaseTypes.IJsonDocument array) =
        let texts = Array.map extractText xs
        String.Join(separator, texts)

    let formatWithCommas (i: int) =
        i.ToString("N0", CultureInfo.InvariantCulture)

    let serializeToJson items =
        let serializerOptions = JsonSerializerOptions()
        serializerOptions.WriteIndented <- true
        serializerOptions.Encoder <- JavaScriptEncoder.Create UnicodeRanges.All
        JsonSerializer.Serialize(items, serializerOptions)

module IO =
    open Errors

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
        | e -> Error (IoError e.Message)

    let createBackUpFilePath (cachedTagFile: FileInfo) =
        let baseName = Path.GetFileNameWithoutExtension cachedTagFile.Name
        let timestamp = DateTimeOffset.Now.ToString "yyyyMMdd_HHmmss"
        let extension = cachedTagFile.Extension // Includes the initial period.
        let fileName = sprintf "%s-%s%s" baseName timestamp extension
        Path.Combine(cachedTagFile.DirectoryName, fileName)

    let writeBackupFile (cachedTagFile: FileInfo) =
        if cachedTagFile.Exists
        then
            try
                cachedTagFile
                |> createBackUpFilePath
                |> cachedTagFile.CopyTo
                |> Some
                |> Ok
            with
            | e -> Error (IoError $"Could not create backup file: {e.Message}")
        else Ok None

module Tags =
    open IO
    open Utilities

    type FileTags =
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
        | NoChange
        | Updated
        | Added

    type CheckResultWithFileTags = CheckResult * FileTags

    [<Literal>]
    let private tagSample = """
    [
      {
        "FileNameOnly": "",
        "DirectoryName": "",
        "Artists": [""],
        "AlbumArtists": [""],
        "Album": "",
        "TrackNo": 0,
        "Title": "",
        "Year": 0,
        "Genres": [""],
        "Duration": "00:00:00",
        "LastWriteTime": "2023-09-13T13:49:44+09:00"
      }
    ]"""

    type CachedTags = JsonProvider<tagSample>
    type FileNameWithCachedTags = Map<string, JsonProvider<tagSample>.Root>

    let parseCachedTagData filePath =
        CachedTags.Parse filePath

    let audioFilePath (tags: CachedTags.Root) =
        Path.Combine [| extractText tags.DirectoryName; extractText tags.FileNameOnly |]

    let compareAndUpdateTagData
        (cachedTags: FileNameWithCachedTags)
        (fileInfos: FileInfo seq)
        : CheckResultWithFileTags seq
        =
        let createNewTagData (fileInfo: FileInfo) =
            let newestTags = readFileTags fileInfo.FullName

            if newestTags.Tag = null
            then
                {
                    FileNameOnly = fileInfo.Name
                    DirectoryName = fileInfo.DirectoryName
                    Artists = [|String.Empty|]
                    AlbumArtists = [|String.Empty|]
                    Album = String.Empty
                    TrackNo = 0u
                    Title = String.Empty
                    Year = 0u
                    Genres = [|String.Empty|]
                    Duration = TimeSpan.Zero
                    LastWriteTime = fileInfo.LastWriteTime |> DateTimeOffset
                }
            else
                {
                    FileNameOnly = fileInfo.Name
                    DirectoryName = fileInfo.DirectoryName
                    Artists = if newestTags.Tag.Performers = null then [|String.Empty|] else newestTags.Tag.Performers |> Array.map (fun p -> p.Normalize())
                    AlbumArtists = if newestTags.Tag.AlbumArtists = null then [|String.Empty|] else newestTags.Tag.AlbumArtists |> Array.map (fun p -> p.Normalize())
                    Album = if newestTags.Tag.Album = null then String.Empty else newestTags.Tag.Album.Normalize()
                    TrackNo = newestTags.Tag.Track
                    Title = newestTags.Tag.Title
                    Year = newestTags.Tag.Year
                    Genres = newestTags.Tag.Genres
                    Duration = newestTags.Properties.Duration
                    LastWriteTime = fileInfo.LastWriteTime |> DateTimeOffset
                }

        let useExistingTagData (cached: CachedTags.Root) =
            {
                FileNameOnly = cached.FileNameOnly |> extractText
                DirectoryName = cached.DirectoryName |> extractText
                Artists = cached.Artists |> Array.map extractText
                AlbumArtists = cached.AlbumArtists |> Array.map extractText
                Album = cached.Album |> extractText
                TrackNo = uint cached.TrackNo
                Title = cached.Title |> extractText
                Year = uint cached.Year
                Genres = cached.Genres |> Array.map extractText
                Duration = cached.Duration
                LastWriteTime = DateTimeOffset cached.LastWriteTime.DateTime
            }

        let updateTags (cachedTags: FileNameWithCachedTags) (fileInfo: FileInfo) : CheckResultWithFileTags =
            if Map.containsKey fileInfo.FullName cachedTags
            then
                let fileCachedTags = Map.find fileInfo.FullName cachedTags
                if fileCachedTags.LastWriteTime.DateTime < fileInfo.LastWriteTime
                then Updated, (createNewTagData fileInfo)
                else NoChange, (useExistingTagData fileCachedTags)
            else Added, (createNewTagData fileInfo)

        fileInfos
        |> Seq.map (fun fileInfo -> updateTags cachedTags fileInfo)

    let reportResults (results: CheckResultWithFileTags seq) =
        let initialCounts = {| Added = 0; Updated = 0; NoChange = 0 |}

        let totals =
            (initialCounts, results |> Seq.map fst)
            ||> Seq.fold (fun acc r ->
                match r with
                | Added -> {| acc with Added = acc.Added + 1 |}
                | Updated -> {| acc with Updated = acc.Updated + 1 |}
                | NoChange -> {| acc with NoChange = acc.NoChange + 1 |})

        printfn "Results:"
        printfn "• Created:  %d" totals.Added
        printfn "• Updated:  %d" totals.Updated
        printfn "• NoChange: %d" totals.NoChange

        results

open Errors
open IO
open Tags
open Utilities

let run () =
    result {
        let! mediaDir, cachedTagFile = ArgValidation.validate
        let! fileInfos = getFileInfos mediaDir

        let cachedTagMap =
            if cachedTagFile.Exists
            then
                cachedTagFile.FullName
                |> System.IO.File.ReadAllText
                |> parseCachedTagData
                |> Array.map (fun tags -> audioFilePath tags, tags)
                |> Map.ofArray
            else
                Map.empty

        let newJson =
            fileInfos
            |> compareAndUpdateTagData cachedTagMap
            |> reportResults
            |> Seq.map snd
            |> serializeToJson

        let! backUpFile = writeBackupFile cachedTagFile
        backUpFile |> Option.iter (fun file -> printfn "Backed up previous tag file to \"%s\"." file.Name)

        do! writeFile cachedTagFile.FullName newJson
    }

let watch = Startwatch.Library.Watch()

match run () with
| Ok _ ->
    printfn $"Done in {watch.ElapsedFriendly}."
    0
| Error e ->
    printfn "%s" (message e)
    printfn $"Failed after {watch.ElapsedFriendly}."
    1
