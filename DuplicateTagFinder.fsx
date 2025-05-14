#r "nuget: FsToolkit.ErrorHandling"
#r "nuget: FSharp.Data"

open System
open System.Globalization
open FSharp.Data

module Utilities =
    let extractText (x: Runtime.BaseTypes.IJsonDocument) =
        x.JsonValue.InnerText()

    let joinWithSeparator (separator: string) (xs: Runtime.BaseTypes.IJsonDocument array) =
        let texts = Array.map extractText xs
        String.Join(separator, texts)

    let formatWithCommas (i: int) =
        i.ToString("N0", CultureInfo.InvariantCulture)

module Settings =
    [<Literal>]
    let private settingsPath = "settings.json"

    type private Settings = JsonProvider<settingsPath>

    type ExclusionPair =
        { Artist: string option
          Title: string option }

    type SettingsType =
        { Exclusions: ExclusionPair array
          ArtistReplacements: string array
          TitleReplacements: string array }

    let private toSettings (settings: Settings.Root) =
        { Exclusions =
              settings.Exclusions
              |> Array.map (fun x -> { Artist = x.Artist
                                       Title = x.Title })
          ArtistReplacements = settings.ArtistReplacements
          TitleReplacements = settings.TitleReplacements }

    let load () = Settings.Load settingsPath |> toSettings

    let summarize settings =
        printfn $"Exclusions:          %d{settings.Exclusions.Length}"
        printfn $"Artist Replacements: %d{settings.ArtistReplacements.Length}"
        printfn $"Title Replacements:  %d{settings.TitleReplacements.Length}"

module Tags =
    // open Utilities

    [<Literal>]
    let private tagSample = """
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

    // type TaggedFileInfo =
    //     { FileName: string
    //       DirectoryName: string
    //       Artists: string array
    //       AlbumArtists: string array
    //       Album: string
    //       TrackNo: int
    //       Title: string
    //       Year: int
    //       Genres: string array
    //       Duration: TimeSpan
    //       LastWriteTime: DateTimeOffset }

    // let private toTaggedFileInfo (fileTags: CachedTags.Root) =
    //     let toFsharpArray coll =
    //         coll |> Seq.cast |> Seq.toArray
    //
    //     { FileName = fileTags.FileName |> extractText
    //       DirectoryName = fileTags.DirectoryName |> extractText
    //       Artists = fileTags.Artists |> toFsharpArray
    //       AlbumArtists = fileTags.AlbumArtists |> toFsharpArray
    //       Album = fileTags.Album |> extractText
    //       TrackNo = fileTags.TrackNo
    //       Title = fileTags.Title |> extractText
    //       Year = fileTags.Year
    //       Genres = fileTags.Genres |> toFsharpArray
    //       Duration = fileTags.Duration
    //       LastWriteTime = fileTags.LastWriteTime }

module Exclusions =
    open Utilities
    open Settings
    open Tags

    let excludeFile (file: CachedTags.Root) (settings: SettingsType) =
        let contains (target: string) (collection: string seq) =
            collection
            |> Seq.exists (fun x -> StringComparer.InvariantCultureIgnoreCase.Equals(x, target))

        let tags =
            {| AlbumArtists = Seq.map extractText file.AlbumArtists
               Artists = Seq.map extractText file.Artists
               Title = extractText file.Title |}

        let isExcluded rule =
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
        |> Array.exists isExcluded

module Modifications =
    let removeSubstrings (substrings: string array) (text: string) : string =
        Array.fold
            (fun acc x -> acc.Replace(x, String.Empty))
            text
            substrings

open System.IO
open Settings
open Tags
open Utilities
open Exclusions
open Modifications

try
    if fsi.CommandLineArgs.Length <> 2
    then printfn "Bad arguments!"
    elif not (File.Exists fsi.CommandLineArgs[1])
    then printfn "Tag file is missing!"

    let cachedTagFileName = fsi.CommandLineArgs[1] // TODO: Add proper validation.

    let settings = Settings.load ()
    summarize settings

    let rawTagJson = System.IO.File.ReadAllText cachedTagFileName
    let allTags = CachedTags.Parse rawTagJson
    printfn $"Total file count:    %s{formatWithCommas allTags.Length}"

    let filteredTags = allTags |> Array.filter (fun x -> not <| excludeFile x settings)
    printfn $"Filtered file count: %s{formatWithCommas filteredTags.Length}"

    filteredTags
    |> Array.filter (fun track ->
        let hasArtists = track.Artists.Length > 0
        let titleText = extractText track.Title
        let hasTitle = not (String.IsNullOrWhiteSpace titleText)
        hasArtists && hasTitle)
    |> Array.groupBy (fun track ->
        let artists = track.Artists
                      |> Array.map extractText
                      |> String.Concat
                      |> removeSubstrings settings.ArtistReplacements
        let title = track.Title
                    |> extractText
                    |> removeSubstrings settings.TitleReplacements
        $"{artists}{title}")
    |> Array.filter (fun (_, groupedTracks) -> groupedTracks.Length > 1)
    |> Array.iteri (fun i groupedTracks ->
        let artists =
            groupedTracks
            |> snd
            |> Array.head
            |> _.Artists
            |> joinWithSeparator ", "
        printfn $"{i + 1}. {artists}"

        snd groupedTracks
        |> Array.iter (fun x -> printfn $"""   • {x.Title}"""))
with
| e ->
    printfn $"ERROR: {e.Message}"
    printfn $"ERROR: {e.StackTrace}"
