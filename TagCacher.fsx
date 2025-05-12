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

type Errors =
    | InvalidArgCount
    | MediaDirectoryMissing
    | IoError of string

module ArgValidation =
    let validate =
        if fsi.CommandLineArgs.Length <> 3 // Index 0 is the stript nameof.
        then Error InvalidArgCount
        else
            let mediaDir, cachedTagFile = DirectoryInfo fsi.CommandLineArgs[1], FileInfo fsi.CommandLineArgs[2]
            if not mediaDir.Exists
            then Error MediaDirectoryMissing
            else Ok (mediaDir, cachedTagFile)

module Utilities =
    let extractText (x: Runtime.BaseTypes.IJsonDocument) =
        x.JsonValue.InnerText()

    let joinWithSeparator (separator: string) (xs: Runtime.BaseTypes.IJsonDocument array) =
        let texts = Array.map extractText xs
        String.Join(separator, texts)

    let formatWithCommas (i: int) =
        i.ToString("N0", CultureInfo.InvariantCulture)

module IO =
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

    let createBackUpFilePath (fileInfo: FileInfo) =
        let baseName = Path.GetFileNameWithoutExtension fileInfo.Name
        let timestamp = DateTimeOffset.Now.ToString "yyyyMMdd_HHmmss"
        let extension = fileInfo.Extension
        let fileName = sprintf "%s-%s%s" baseName timestamp extension
        Path.Combine(fileInfo.DirectoryName, fileName)

module Tags =
    type FileTags =
        { FileNameOnly: string
          DirectoryName: string
          Artists: string array
          AlbumArtists: string array
          Album: string
          TrackNo: uint
          Title: string
          Year: uint
          Genres: string array
          Duration: TimeSpan
          LastWriteTime: DateTimeOffset }

    [<Literal>]
    let tagSample = """
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

    let getCachedData filePath =
        CachedTags.Parse filePath

open IO
open Tags
open Utilities
open System.Text.Json
open System.Text.Encodings.Web
open System.Text.Unicode

let compareWithCachedTags (cachedTags: Map<string, JsonProvider<tagSample>.Root>) (fileInfos: seq<FileInfo>) =
    let createNewTagData (fileInfo: FileInfo) =
        let newestTags = readFileTags fileInfo.FullName

        if newestTags.Tag = null
        then
            { FileNameOnly = fileInfo.Name
              DirectoryName = fileInfo.DirectoryName
              Artists = [|String.Empty|]
              AlbumArtists = [|String.Empty|]
              Album = String.Empty
              TrackNo = 0u
              Title = String.Empty
              Year = 0u
              Genres = [|String.Empty|]
              Duration = TimeSpan.Zero
              LastWriteTime = DateTimeOffset fileInfo.LastWriteTime }
        else
            { FileNameOnly = fileInfo.Name
              DirectoryName = fileInfo.DirectoryName
              Artists = if newestTags.Tag.Performers = null then [|String.Empty|] else newestTags.Tag.Performers |> Array.map (fun p -> p.Normalize())
              AlbumArtists = if newestTags.Tag.AlbumArtists = null then [|String.Empty|] else newestTags.Tag.AlbumArtists |> Array.map (fun p -> p.Normalize())
              Album = if newestTags.Tag.Album = null then String.Empty else newestTags.Tag.Album.Normalize()
              TrackNo = newestTags.Tag.Track
              Title = newestTags.Tag.Title
              Year = newestTags.Tag.Year
              Genres = newestTags.Tag.Genres
              Duration = newestTags.Properties.Duration
              LastWriteTime = DateTimeOffset fileInfo.LastWriteTime }

    let useExistingTagData (cached: JsonProvider<tagSample>.Root) =
        { FileNameOnly = cached.FileNameOnly |> extractText
          DirectoryName = cached.DirectoryName |> extractText
          Artists = cached.Artists |> Array.map extractText
          AlbumArtists = cached.AlbumArtists |> Array.map extractText
          Album = cached.Album |> extractText
          TrackNo = uint cached.TrackNo
          Title = cached.Title |> extractText
          Year = uint cached.Year
          Genres = cached.Genres |> Array.map extractText
          Duration = cached.Duration
          LastWriteTime = DateTimeOffset cached.LastWriteTime.DateTime }

    fileInfos
    |> Seq.map (fun fileInfo ->
        if Map.containsKey fileInfo.FullName cachedTags
        then
            let fileCachedTags = Map.find fileInfo.FullName cachedTags
            if fileCachedTags.LastWriteTime.DateTime < fileInfo.LastWriteTime
            then createNewTagData fileInfo
            else useExistingTagData fileCachedTags
        else createNewTagData fileInfo)

let run () =
    result {
        let! mediaDir, cachedTagFile = ArgValidation.validate
        let! fileInfos = getFileInfos mediaDir

        let cachedTagMap =
            if cachedTagFile.Exists
            then
                let cachedTagJson = System.IO.File.ReadAllText cachedTagFile.FullName
                let cachedTags: JsonProvider<tagSample>.Root array = getCachedData cachedTagJson
                cachedTags
                |> Array.map (fun t -> Path.Combine [| extractText t.DirectoryName; extractText t.FileNameOnly |], t)
                |> Map.ofArray
            else
                Map.empty

        let updatedTags = fileInfos |> compareWithCachedTags cachedTagMap

        let serializerOptions = JsonSerializerOptions()
        serializerOptions.WriteIndented <- true
        serializerOptions.Encoder <- JavaScriptEncoder.Create UnicodeRanges.All
        let newJson = JsonSerializer.Serialize(updatedTags, serializerOptions)

        // Back up the old file.
        if cachedTagFile.Exists then
            let backedUpFile = cachedTagFile.CopyTo(createBackUpFilePath cachedTagFile)
            printfn "Backed up previous cached tags to \"%s\"." backedUpFile.Name

        File.WriteAllText(cachedTagFile.FullName, newJson)
    }

let watch = Startwatch.Library.Watch()
match run () with
| Ok _ ->
    printfn $"Done in {watch.ElapsedFriendly}"
    0
| Error e ->
    printfn "ERROR: %A" e
    printfn $"Failed after {watch.ElapsedFriendly}"
    1
