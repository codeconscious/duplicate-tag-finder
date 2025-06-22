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

let findDuplicates
    (settings: SettingsRoot)
    (tags: FilteredTagCollection)
    : Map<string, FilteredTagCollection>
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
    |> Map.ofArray

let printTotalCount (tags: FileTagCollection) =
    printfn $"Total file count:    %s{formatNumber tags.Length}"

let printFilteredCount (tags: FilteredTagCollection) =
    printfn $"Filtered file count: %s{formatNumber tags.Length}"

let printResults (groupedTracks: Map<string, FilteredTagCollection>) =
    if groupedTracks.IsEmpty
    then printfn "No duplicates found."
    else
        groupedTracks
        |> Map.values
        |> Array.ofSeq
        |> Array.iteri (fun i groupTracks ->
            // Print the artist from this group's first file's artists.
            groupTracks
            |> Array.head
            |> _.Artists
            |> fun x -> String.Join(", ", x)
            |> printfn "%d. %s" (i + 1) // Start at 1.

            // Print each possible-duplicate track in the group.
            groupTracks
            |> Array.iter (fun x -> printfn $"""   • {x.Title}"""))
