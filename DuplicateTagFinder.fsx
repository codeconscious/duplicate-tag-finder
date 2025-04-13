#r "nuget: FsToolkit.ErrorHandling"
#r "nuget: FSharp.Data"

open FSharp.Data

// let json = System.IO.File.ReadAllText("settings.json")
//
// type Settings = JsonProvider<json>

[<Literal>]
let samplePath = "settings.json"

type Settings = JsonProvider<samplePath>

let data = Settings.Load(samplePath)
printfn $"Cached Tag File:     %s{data.CachedTagFile}"
printfn $"Exclusions:          %d{data.Exclusions.Length}"
printfn $"Artist Replacements: %d{data.ArtistReplacements.Length}"
printfn $"Title Replacements:  %d{data.TitleReplacements.Length}"

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
let json = System.IO.File.ReadAllText(data.CachedTagFile)
let tagData =
    try
        Ok <| CachedTags.Parse(json)
    with
    | e -> Error e

match tagData with
| Ok data -> printfn $"Tag count: %d{data.Length}"
| Error e -> printfn $"ERROR: {e.Message}"
