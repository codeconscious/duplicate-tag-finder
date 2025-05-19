#r "nuget: FsToolkit.ErrorHandling"
#r "nuget: FSharp.Data"
#r "nuget: CodeConscious.Startwatch, 1.0.0"

open System
open System.Globalization
open System.IO
open FSharp.Data
open FsToolkit.ErrorHandling

module Errors =
    type Error =
        | InvalidArgCount
        | FileMissing of string
        | IoError of string
        | SettingsParseError of string
        | TagParseError of string

    let message: Error -> string = function
        | InvalidArgCount -> "Invalid arguments. Pass in two JSON filename paths: (1) your settings file and (2) your cached tag data."
        | FileMissing fileName -> $"The file \"{fileName}\" was not found."
        | IoError msg -> $"I/O failure: {msg}"
        | SettingsParseError msg -> $"Unable to parse the settings file: {msg}"
        | TagParseError msg -> $"Unable to parse the tag file: {msg}"

module Operators =
    let (>>=) result func = Result.bind func result
    let (<!>) result func = Result.map func result
    let (<.>) result func = Result.tee func result

module Utilities =
    let formatNumber (i: int) =
        i.ToString("N0", CultureInfo.InvariantCulture) // Sample: 1,000

    let removeSubstrings (substrings: string array) (text: string) : string =
        Array.fold
            (fun acc x -> acc.Replace(x, String.Empty))
            text
            substrings

module IO =
    open Errors
    open Operators

    let readFile (fileName: string) : Result<string, Error> =
        try
            fileName
            |> System.IO.File.ReadAllText
            |> Ok
        with
        | :? FileNotFoundException -> Error (FileMissing fileName)
        | e -> Error (IoError e.Message)

module ArgValidation =
    open Errors

    let validate (args: string array) : Result<string * string, Error> =
        if args.Length <> 3 // Index 0 is the name of the script itself.
        then Error InvalidArgCount
        else Ok (args[1], args[2])

module Settings =
    open Errors

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

module Tags =
    open Errors
    open Utilities
    open Settings

    [<Literal>]
    let private tagSample = """
    [
      {
        "FileName": "text",
        "DirectoryName": "text",
        "Artists": ["text"],
        "AlbumArtists": ["text"],
        "Album": "text",
        "TrackNo": 0,
        "Title": "text",
        "Year": 0,
        "Genres": ["text"],
        "Duration": "00:00:00",
        "LastWriteTime": "2023-09-13T13:49:44+09:00"
      }
    ]"""

    type TagJsonProvider = JsonProvider<tagSample>
    type FileTags = TagJsonProvider.Root
    type TagCollection = FileTags array
    type FilteredTagCollection = FileTags array

    let parseToTags (json: string) : Result<TagCollection, Error> =
        try
            json
            |> TagJsonProvider.Parse
            |> Ok
        with
        | e -> Error (TagParseError e.Message)

    let filter (settings: SettingsRoot) (allTags: TagCollection) : FileTags array =
        let excludeFile (settings: SettingsRoot) (file: FileTags) =
            let contains (target: string) (collection: string seq) =
                collection
                |> Seq.exists (fun x -> StringComparer.InvariantCultureIgnoreCase.Equals(x, target))

            let isExcluded (rule: SettingsProvider.Exclusion) =
                match rule.Artist, rule.Title with
                | Some excludedArtist, Some excludedTitle ->
                    (contains excludedArtist file.AlbumArtists || contains excludedArtist file.Artists) &&
                    file.Title.StartsWith(excludedTitle, StringComparison.InvariantCultureIgnoreCase)
                | Some excludedArtist, None ->
                    contains excludedArtist file.AlbumArtists || contains excludedArtist file.Artists
                | None, Some excludedTitle ->
                    file.Title.StartsWith(excludedTitle, StringComparison.InvariantCultureIgnoreCase)
                | _ -> false

            settings.Exclusions
            |> Array.exists isExcluded

        allTags
        |> Array.filter (fun x -> not <| excludeFile settings x)

    let findDuplicates
        (settings: SettingsRoot)
        (tags: FilteredTagCollection)
        : (string * FilteredTagCollection) array
        =
        tags
        |> Array.filter (fun track ->
            let hasArtists = track.Artists.Length > 0
            let hasTitle = not <| String.IsNullOrWhiteSpace track.Title
            hasArtists && hasTitle)
        |> Array.groupBy (fun track ->
            let artists =
                track.Artists
                |> String.Concat
                |> removeSubstrings settings.ArtistReplacements
            let title =
                track.Title
                |> removeSubstrings settings.TitleReplacements
            $"{artists}{title}")
        |> Array.filter (fun (_, groupedTracks) -> groupedTracks.Length > 1)

    let printTotalCount (tags: TagCollection) =
        printfn $"Total file count:    %s{formatNumber tags.Length}"

    let printFilteredCount (tags: FilteredTagCollection) =
        printfn $"Filtered file count: %s{formatNumber tags.Length}"

    let printResults (groupedTracks: (string * FilteredTagCollection) array) =
        if groupedTracks.Length = 0
        then printfn "No duplicates found."
        else
            groupedTracks
            |> Array.iteri (fun i groupTracks ->
                // Print the artist(s) using the group's first file's artist(s).
                groupTracks
                |> snd
                |> Array.head
                |> _.Artists
                |> (fun x -> String.Join(", ", x))
                |> printfn "%d. %s" (i + 1) // Start at 1.

                // Print each possible-duplicate track in the group.
                groupTracks
                |> snd
                |> Array.iter (fun x -> printfn $"""   • {x.Title}"""))

open Operators
open ArgValidation
open Errors
open IO
open Tags
open Settings

let run () =
    result {
        let! settingsFile, cachedTagFile = validate fsi.CommandLineArgs

        let! settings =
            settingsFile
            |> readFile
            >>= parseToSettings
            <.> printSummary

        return
            cachedTagFile
            |> readFile
            >>= parseToTags
            <.> printTotalCount
            <!> filter settings
            <.> printFilteredCount
            <!> findDuplicates settings
            <.> printResults
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
