#r "nuget: TagLibSharp"
#r "nuget: FsToolkit.ErrorHandling"
#r "nuget: FSharp.Data"

open System
open System.Globalization
open FSharp.Data
open FsToolkit.ErrorHandling
open System
open System.IO

module Utilities =
    let extractText (x: Runtime.BaseTypes.IJsonDocument) =
        x.JsonValue.InnerText()

    let joinWithSeparator (separator: string) (xs: Runtime.BaseTypes.IJsonDocument array) =
        let texts = Array.map extractText xs
        String.Join(separator, texts)

    let formatWithCommas (i: int) =
        i.ToString("N0", CultureInfo.InvariantCulture)

module Files =
    type Errors =
        | DirectoryMissing

    let getFileInfos dirPath =
        let isSupportedAudioFile (fileInfo: FileInfo) =
            [".mp3"; ".m4a"; "mp4"; ".ogg"; ".flac"]
            |> List.contains fileInfo.Extension

        // TODO: Add try block.
        if not (Directory.Exists dirPath)
        then Error DirectoryMissing
        else
            Directory.EnumerateFiles(dirPath, "*", SearchOption.AllDirectories)
            |> Seq.map (fun item -> FileInfo item)
            |> Seq.filter isSupportedAudioFile
            |> Ok

    let readFileTags (filePath: string) : TagLib.File =
        TagLib.File.Create filePath

module Tags =
    open Utilities

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
          LastWriteTime: DateTime }

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

open Files
open Tags
open Utilities
open System.Text.Json
open System.Text.Encodings.Web
open System.Text.Unicode

let compareWithCachedTags (cachedTags: Map<string, JsonProvider<tagSample>.Root>) (fileInfos: seq<FileInfo>) =
    let createTagEntry (fileInfo: FileInfo) =
        let newestTags = readFileTags fileInfo.FullName

        let setDefaultIfNull x defaultValue =
            match x with
            | null -> defaultValue
            | _ -> x

        // printfn "%A" fileInfo.FullName // TODO: DELETE

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
              LastWriteTime = fileInfo.LastWriteTime }
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
              LastWriteTime = fileInfo.LastWriteTime }

    let cachedTagInfoToNew (cached: JsonProvider<tagSample>.Root) =
        { FileNameOnly = extractText cached.FileNameOnly
          DirectoryName = extractText cached.DirectoryName
          Artists = cached.Artists |> Array.map extractText
          AlbumArtists = cached.AlbumArtists |> Array.map extractText
          Album = cached.Album |> extractText
          TrackNo = uint cached.TrackNo
          Title = cached.Title |> extractText
          Year = uint cached.Year
          Genres = cached.Genres |> Array.map extractText
          Duration = cached.Duration
          LastWriteTime = cached.LastWriteTime.DateTime }

    fileInfos
    |> Seq.map (fun fileInfo ->
        if Map.containsKey fileInfo.FullName cachedTags // TODO: Redo using tryFind, etc.
        then
            let thisFileTags = Map.find fileInfo.FullName cachedTags
            if thisFileTags.LastWriteTime.DateTime < fileInfo.LastWriteTime
            then createTagEntry fileInfo
            else cachedTagInfoToNew thisFileTags
        else createTagEntry fileInfo)

let doIt =
    result {
        let! fileInfos = getFileInfos fsi.CommandLineArgs[1]
        let cachedTagJsonPath = fsi.CommandLineArgs[2]

        let cachedTagJson = System.IO.File.ReadAllText cachedTagJsonPath
        let cachedTags: JsonProvider<tagSample>.Root array = CachedTags.Parse cachedTagJson
        let cachedTagMap =
            cachedTags
            |> Array.map (fun t -> Path.Combine [| extractText t.DirectoryName; extractText t.FileNameOnly |], t)
            |> Map.ofArray

        let newTags = fileInfos |> compareWithCachedTags cachedTagMap
        let options = JsonSerializerOptions()
        options.WriteIndented <- true
        options.Encoder <- JavaScriptEncoder.Create UnicodeRanges.All
        let newJson = JsonSerializer.Serialize(newTags, options)

        let tempPath = Path.GetTempPath()
        printfn "Temp path: %s" tempPath
        File.WriteAllText(Path.Combine(tempPath, "test.json"), newJson)
    }

match doIt with
| Ok _ -> 0
| Error e ->
    printfn "%A" e
    1
