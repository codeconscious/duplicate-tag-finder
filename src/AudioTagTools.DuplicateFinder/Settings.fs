module Settings

open Errors
open FSharp.Data

[<Literal>]
let settingsSample = """
{
    "exclusions": [
        {
            "artist": "artist"
        },
        {
            "title": "title"
        },
        {
            "artist": "artist",
            "title": "title"
        }
    ],
    "artistReplacements": [
        "text"
    ],
    "titleReplacements": [
        "text"
    ]
}
"""

type SettingsProvider = JsonProvider<settingsSample>
type SettingsRoot = SettingsProvider.Root

let parseToSettings (json: string) : Result<SettingsRoot, Error> =
    try
        json
        |> SettingsProvider.Parse
        |> Ok
    with
    | e -> Error (SettingsParseError e.Message)

let printSummary (settings: SettingsRoot) =
    printfn "Settings summary:"
    printfn $"  Exclusions:          %d{settings.Exclusions.Length}"
    printfn $"  Artist Replacements: %d{settings.ArtistReplacements.Length}"
    printfn $"  Title Replacements:  %d{settings.TitleReplacements.Length}"
