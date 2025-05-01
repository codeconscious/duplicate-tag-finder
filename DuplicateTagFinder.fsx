#r "nuget: FsToolkit.ErrorHandling"
#r "nuget: FSharp.Data"

open System
open FSharp.Data

[<Literal>]
let settingsPath = "settings.json"

type Settings = JsonProvider<settingsPath>

type ExclusionPair = { Artist: string option; Title: string option }

type SettingsType =
    { CachedTagFile: string
      Exclusions: ExclusionPair array
      ArtistReplacements: string array
      TitleReplacements: string array }

let toSettings (x: Settings.Root) =
    { CachedTagFile = x.CachedTagFile
      Exclusions =
          x.Exclusions
          // |> Seq.cast
          // |> Seq.toArray
          |> Array.map (fun y -> { Artist = y.Artist; Title = y.Title })
      ArtistReplacements = x.ArtistReplacements
      TitleReplacements = x.TitleReplacements }

let settings = Settings.Load(settingsPath) |> toSettings

printfn $"Cached Tag File:     %s{settings.CachedTagFile}"
printfn $"Exclusions:          %d{settings.Exclusions.Length}"
printfn $"Artist Replacements: %d{settings.ArtistReplacements.Length}"
printfn $"Title Replacements:  %d{settings.TitleReplacements.Length}"

[<Literal>]
let tagSample = """
[
  {
    "FileName": "",
    "DirectoryName": "",
    "Artists": [],
    "AlbumArtists": [],
    "Album": "",
    "TrackNo": 0,
    "Title": "",
    "Year": 0,
    "Genres": [],
    "Duration": "00:00:00",
    "LastWriteTime": "2023-09-13T13:49:44+09:00"
  }
]"""

type CachedTags = JsonProvider<tagSample>

type TaggedFileInfo =
    { FileName: string
      DirectoryName: string
      Artists: string array
      AlbumArtists: string array
      Album: string
      TrackNo: int // uint?
      Title: string
      Year: int
      Genres: string array
      Duration: TimeSpan
      LastWriteTime: DateTimeOffset }

let toTaggedFileInfo (x: CachedTags.Root) =
    let convertToFsharpArray coll =
        coll |> Seq.cast |> Seq.toArray

    { FileName = x.FileName.JsonValue.ToString() // ?
      DirectoryName = x.DirectoryName.JsonValue.ToString() // ?
      Artists = x.Artists |> convertToFsharpArray
      AlbumArtists = x.AlbumArtists |> convertToFsharpArray
      Album = x.Album.JsonValue.ToString() // ?
      TrackNo = x.TrackNo
      Title = x.Title.JsonValue.ToString() // ?
      Year = x.Year
      Genres = x.Genres |> convertToFsharpArray
      Duration = x.Duration
      LastWriteTime = x.LastWriteTime }

// let checkExcludeFile (file: TaggedFileInfo) (settings: SettingsType) =
let excludeFile (file: CachedTags.Root) (settings: SettingsType) =
    let containsCaseInsensitive (value: string) (arr: string seq) =
        arr
        |> Seq.exists (fun x -> StringComparer.InvariantCultureIgnoreCase.Equals(x, value))

    let albumArtists = file.AlbumArtists |> Seq.map (fun x -> x.JsonValue.InnerText())
    let artists = file.Artists |> Seq.map (fun x -> x.JsonValue.InnerText())
    let title = file.Title.JsonValue.InnerText()

    settings.Exclusions
    |> Array.exists (fun exclusion ->
         match exclusion.Artist, exclusion.Title with
         | Some excludedArtist, Some excludedTitle ->
             (albumArtists |> containsCaseInsensitive excludedArtist ||
              artists |> containsCaseInsensitive excludedArtist) &&
             title.StartsWith(excludedTitle, StringComparison.InvariantCultureIgnoreCase)
         | Some excludedArtist, None ->
             (albumArtists |> containsCaseInsensitive excludedArtist ||
              artists |> containsCaseInsensitive excludedArtist)
         | None, Some excludedTitle ->
             title.StartsWith(excludedTitle, StringComparison.InvariantCultureIgnoreCase)
         | _ -> false)

try
    let rawTagJson = System.IO.File.ReadAllText settings.CachedTagFile
    let parsedTags = CachedTags.Parse rawTagJson
    printfn $"Total file count: %d{parsedTags.Length}"
    let filtered = parsedTags |> Array.filter (fun x -> not <| excludeFile x settings)
    printfn $"Filtered file count: %d{filtered.Length}"
with
| e ->
    printfn $"ERROR: {e.Message}"
    printfn $"ERROR: {e.StackTrace}"

