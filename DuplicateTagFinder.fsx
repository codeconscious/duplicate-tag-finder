#r "nuget: FsToolkit.ErrorHandling"
#r "nuget: FSharp.Data"

open System
open FSharp.Data


[<Literal>]
let samplePath = "settings.json"

type Settings = JsonProvider<samplePath>

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
          |> Seq.cast
          |> Seq.toArray
          |> Array.map (fun y -> { Artist = y.Artist; Title = y.Title })
      ArtistReplacements = x.ArtistReplacements
      TitleReplacements = x.TitleReplacements }

let settings = Settings.Load(samplePath)// |> toSettings

printfn $"Cached Tag File:     %s{settings.CachedTagFile}"
printfn $"Exclusions:          %d{settings.Exclusions.Length}"
printfn $"Artist Replacements: %d{settings.ArtistReplacements.Length}"
printfn $"Title Replacements:  %d{settings.TitleReplacements.Length}"

[<Literal>]
let tagSample = """
[
  {
    "Artists": [],
    "AlbumArtists": [],
    "Album": "",
    "TrackNo": 0,
    "Title": "",
    "Year": 0,
    "Genres": [],
    "Duration": "00:00:00",
    "UpdatedAt": "2023-09-13T13:49:44+09:00"
  }
]"""

type CachedTags = JsonProvider<tagSample>

type TaggedFileInfo =
    { Artists: string array
      AlbumArtists: string array
      Album: string
      TrackNo: int // uint?
      Title: string
      Year: int
      Genres: string array
      Duration: TimeSpan
      UpdatedAt: DateTimeOffset }

let toTaggedFileInfo (x: CachedTags.Root) =
    let convertToFsharpArray coll =
        coll |> Seq.cast |> Seq.toArray

    { Artists = x.Artists |> convertToFsharpArray
      AlbumArtists = x.AlbumArtists |> convertToFsharpArray
      Album = x.Album.JsonValue.ToString() // ?
      TrackNo = x.TrackNo
      Title = x.Title.JsonValue.ToString() // ?
      Year = x.Year
      Genres = x.Genres |> convertToFsharpArray
      Duration = x.Duration
      UpdatedAt = x.UpdatedAt }

let excludeFile (file: TaggedFileInfo) (settings: SettingsType) =
    let containsCaseInsensitive (value: string) (arr: string array) =
        arr
        |> Array.exists (fun x -> StringComparer.InvariantCultureIgnoreCase.Equals(x, value))

    settings.Exclusions
    // |> Seq.cast
    // |> Seq.toArray
    // |> Array.map (fun y -> { Artist = y.Artist; Title = y.Title })
    |> Seq.exists (fun exclusion ->
         match exclusion.Artist, exclusion.Title with
         | Some artist, Some title ->
             (file.AlbumArtists |> containsCaseInsensitive artist ||
              file.Artists |> containsCaseInsensitive artist) &&
             file.Title.StartsWith(title, StringComparison.InvariantCultureIgnoreCase)
         | Some artist, None ->
             (file.AlbumArtists |> containsCaseInsensitive artist ||
              file.Artists |> containsCaseInsensitive artist)
         | None, Some title ->
             file.Title.StartsWith(title, StringComparison.InvariantCultureIgnoreCase)
         | _ -> false)
    |> (=) false

try
    let rawTagJson = System.IO.File.ReadAllText settings.CachedTagFile
    let parsedTags = CachedTags.Parse rawTagJson
    printfn $"Tag count: %d{parsedTags.Length}"
    // let data = parsedTags |> Array.map toTaggedFileInfo
    // printfn $"Tag count: %d{data.Length}"
    // let filtered = data |> Array.filter (excludeFile data settings.Exclusions)
with
| e ->
    printfn $"ERROR: {e.Message}"
    printfn $"ERROR: {e.StackTrace}"

