#r "nuget: FsToolkit.ErrorHandling"
#r "nuget: FSharp.Data"

open System
open System.Globalization
open FSharp.Data

[<Literal>]
let settingsPath = "settings.json"

type Settings = JsonProvider<settingsPath>

type ExclusionPair =
    { Artist: string option
      Title: string option }

type SettingsType =
    { CachedTagFile: string
      Exclusions: ExclusionPair array
      ArtistReplacements: string array
      TitleReplacements: string array }

let toSettings (root: Settings.Root) =
    { CachedTagFile = root.CachedTagFile
      Exclusions =
          root.Exclusions
          |> Array.map (fun x -> { Artist = x.Artist
                                   Title = x.Title })
      ArtistReplacements = root.ArtistReplacements
      TitleReplacements = root.TitleReplacements }

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
      TrackNo: int
      Title: string
      Year: int
      Genres: string array
      Duration: TimeSpan
      LastWriteTime: DateTimeOffset }

let toTaggedFileInfo (x: CachedTags.Root) =
    let toFsharpArray coll =
        coll |> Seq.cast |> Seq.toArray

    { FileName = x.FileName.JsonValue.InnerText()
      DirectoryName = x.DirectoryName.JsonValue.InnerText()
      Artists = x.Artists |> toFsharpArray
      AlbumArtists = x.AlbumArtists |> toFsharpArray
      Album = x.Album.JsonValue.InnerText()
      TrackNo = x.TrackNo
      Title = x.Title.JsonValue.InnerText()
      Year = x.Year
      Genres = x.Genres |> toFsharpArray
      Duration = x.Duration
      LastWriteTime = x.LastWriteTime }

let excludeFile (file: CachedTags.Root) (settings: SettingsType) =
    let extractValue (x: Runtime.BaseTypes.IJsonDocument) = x.JsonValue.InnerText()

    let contains (value: string) (arr: string seq) =
        arr
        |> Seq.exists (fun x -> StringComparer.InvariantCultureIgnoreCase.Equals(x, value))

    let tags =
        {| AlbumArtists = Seq.map extractValue file.AlbumArtists
           Artists = Seq.map extractValue file.Artists
           Title = extractValue file.Title |}

    let shouldExclude rule =
         match rule.Artist, rule.Title with
         | Some a, Some t ->
             (tags.AlbumArtists |> contains a || tags.Artists |> contains a) &&
             tags.Title.StartsWith(t, StringComparison.InvariantCultureIgnoreCase)
         | Some a, None ->
             (tags.AlbumArtists |> contains a || tags.Artists |> contains a)
         | None, Some t ->
             tags.Title.StartsWith(t, StringComparison.InvariantCultureIgnoreCase)
         | _ -> false

    settings.Exclusions
    |> Array.exists shouldExclude

let formatWithCommas (i: int) = i.ToString("N0", CultureInfo.InvariantCulture)

try
    let rawTagJson = System.IO.File.ReadAllText settings.CachedTagFile
    let allTags = CachedTags.Parse rawTagJson
    printfn $"Total file count:    %s{formatWithCommas allTags.Length}"
    let filteredTags = allTags |> Array.filter (fun x -> not <| excludeFile x settings)
    printfn $"Filtered file count: %s{formatWithCommas filteredTags.Length}"
with
| e ->
    printfn $"ERROR: {e.Message}"
    printfn $"ERROR: {e.StackTrace}"

