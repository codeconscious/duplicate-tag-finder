module Tags

open Errors
open Utilities
open Settings
open System
open FSharp.Data
open Operators

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
        let isExcluded (exclusion: SettingsProvider.Exclusion) =
            match exclusion.Artist, exclusion.Title with
            | Some a, Some t ->
                anyContains [file.AlbumArtists; file.Artists] a &&
                file.Title.StartsWith(t, StringComparison.InvariantCultureIgnoreCase)
            | Some a, None ->
                anyContains [file.AlbumArtists; file.Artists] a
            | None, Some t ->
                file.Title.StartsWith(t, StringComparison.InvariantCultureIgnoreCase)
            | _ -> false

        settings.Exclusions |> Array.exists isExcluded

    allTags |> Array.filter (not << excludeFile settings)

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
        |> Array.iteri (fun i (_, groupTracks) ->
            // Print the artist(s) using the group's first file's artist(s).
            groupTracks
            |> Array.head
            |> _.Artists
            |> fun x -> String.Join(", ", x)
            |> printfn "%d. %s" (i + 1) // Start at 1.

            // Print each possible-duplicate track in the group.
            groupTracks
            |> Array.iter (fun x -> printfn $"""   • {x.Title}"""))
