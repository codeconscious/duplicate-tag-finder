module Tags

open Errors
open Utilities
open Settings
open System
open Operators
open TagLibrary

let parseToTags json =
    parseToTags json
    |> Result.mapError TagParseError

let filter (settings: SettingsRoot) (allTags: FileTagCollection) : FileTags array =
    let excludeFile (settings: SettingsRoot) (fileTags: FileTags) : bool =
        let isExcluded (exclusion: SettingsProvider.Exclusion) : bool =
            match exclusion.Artist, exclusion.Title with
            | Some a, Some t ->
                anyContains [fileTags.AlbumArtists; fileTags.Artists] a &&
                fileTags.Title.StartsWith(t, StringComparison.InvariantCultureIgnoreCase)
            | Some a, None ->
                anyContains [fileTags.AlbumArtists; fileTags.Artists] a
            | None, Some t ->
                fileTags.Title.StartsWith(t, StringComparison.InvariantCultureIgnoreCase)
            | _ -> false

        settings.Exclusions
        |> Array.exists isExcluded

    allTags
    |> Array.filter (not << excludeFile settings)

let private hasArtistOrTitle track =
    let hasAnyArtist (track: FileTags) =
        track.Artists.Length > 0 || track.AlbumArtists.Length > 0

    let hasTitle (track: FileTags) =
        not <| String.IsNullOrWhiteSpace track.Title

    hasAnyArtist track && hasTitle track

let private mainArtists (track: FileTags) =
    match track with
    | t when t.AlbumArtists.Length > 0 -> t.AlbumArtists
    | t -> t.Artists

let private groupName (settings: SettingsRoot) (track: FileTags) =
    // It appears JSON type providers do not import whitespace-only values. Whitespace should
    // always be ignored to increase the accuracy of duplicate checks, so they are added here.
    let removeSubstrings arr =
        arr
        |> Array.append [| " "; "　" |]
        |> removeSubstrings

    let artists =
        track
        |> mainArtists
        |> String.Concat
        |> removeSubstrings settings.ArtistReplacements

    let title =
        track.Title
        |> removeSubstrings settings.TitleReplacements

    $"{artists}{title}"

let findDuplicates (settings: SettingsRoot) (tags: FilteredTagCollection) : FileTags array array =
    tags
    |> Array.filter hasArtistOrTitle
    |> Array.groupBy (groupName settings)
    |> Array.sortBy fst
    |> Array.map snd
    |> Array.filter (fun groupedTracks -> groupedTracks.Length > 1)

let printTotalCount (tags: FileTagCollection) =
    printfn $"Total file count:    %s{formatNumber tags.Length}"

let printFilteredCount (tags: FilteredTagCollection) =
    printfn $"Filtered file count: %s{formatNumber tags.Length}"

let printResults (groupedTracks: FileTags array array) =
    let print i (groupTracks: FileTags array) =
        // Print the joined artists from this group's first file.
        groupTracks
        |> Array.head
        |> mainArtists
        |> String.concat ", "
        |> printfn "%d. %s" (i + 1) // Start at 1, not 0.

        // Print each suspected duplicate track in the group.
        groupTracks
        |> Array.iter (fun x -> printfn $"""   • {String.Join(", ", x.Artists)} / {x.Title}""")

    if Array.isEmpty groupedTracks
    then printfn "No duplicates found."
    else groupedTracks |> Array.iteri print
