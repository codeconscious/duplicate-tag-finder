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
        | ParseError of string

    let message = function
        | InvalidArgCount -> "Invalid arguments. Pass in the path to the JSON file containing your cached tag data."
        | FileMissing fileName -> $"The file \"{fileName}\" was not found."
        | IoError msg -> $"I/O failure: {msg}"
        | ParseError msg -> $"Could not parse the content: {msg}"

module Utilities =
    let extractText (x: Runtime.BaseTypes.IJsonDocument) =
        x.JsonValue.InnerText()

    let joinWithSeparator (separator: string) (xs: Runtime.BaseTypes.IJsonDocument array) =
        let texts = Array.map extractText xs
        String.Join(separator, texts)

    let formatNumber (i: int) =
        i.ToString("N0", CultureInfo.InvariantCulture)

    let removeSubstrings (substrings: string array) (text: string) : string =
        Array.fold
            (fun acc x -> acc.Replace(x, String.Empty))
            text
            substrings

module ArgValidation =
    open Errors

    let validateFilePath () =
        if fsi.CommandLineArgs.Length <> 2 // Index 0 is the name of the script itself.
        then Error InvalidArgCount
        else
            let cachedTagFile = FileInfo fsi.CommandLineArgs[1]

            if cachedTagFile.Exists
            then Ok cachedTagFile
            else Error (FileMissing cachedTagFile.FullName)

module Settings =
    open Errors

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

    // TODO: Move to the IO module?
    let load () =
        try
            Ok (Settings.Load settingsPath |> toSettings)
        with
        | e -> Error (IoError e.Message)

    let summarize (settings: SettingsType) =
        printfn $"Exclusions:          %d{settings.Exclusions.Length}"
        printfn $"Artist Replacements: %d{settings.ArtistReplacements.Length}"
        printfn $"Title Replacements:  %d{settings.TitleReplacements.Length}"

module Tags =
    open Utilities
    open Settings // TODO: Refactor to avoid?

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

    let findDuplicates
        (settings: SettingsType)
        (tags: CachedTags.Root array)
        : (string * CachedTags.Root array) array
        =
        tags
        |> Array.filter (fun track ->
            let hasArtists = track.Artists.Length > 0
            let titleText = extractText track.Title
            let hasTitle = not (String.IsNullOrWhiteSpace titleText)
            hasArtists && hasTitle)
        |> Array.groupBy (fun track ->
            let artists =
                track.Artists
                |> Array.map extractText
                |> String.Concat
                |> removeSubstrings settings.ArtistReplacements
            let title =
                track.Title
                |> extractText
                |> removeSubstrings settings.TitleReplacements
            $"{artists}{title}")
        |> Array.filter (fun (_, groupedTracks) -> groupedTracks.Length > 1)

    let printResults (groupedTracks: (string * CachedTags.Root array) array) =
        groupedTracks
        |> Array.iteri (fun i groupedTracks ->
            // Print the artist(s) using the group's first file's artists.
            groupedTracks
            |> snd
            |> Array.head
            |> _.Artists
            |> joinWithSeparator ", "
            |> printfn "%d. %s" (i + 1)

            // List each possible-duplicate track in the group.
            groupedTracks
            |> snd
            |> Array.iter (fun x -> printfn $"""   • {x.Title}"""))

module IO =
    open Errors
    open Tags

    let readFile (fileInfo: FileInfo) : Result<string, Error> =
        try
            fileInfo.FullName
            |> System.IO.File.ReadAllText
            |> Ok
        with
        | e -> Error (IoError e.Message)

    let parseJson (json: string) : Result<CachedTags.Root array, Error> =
        try
            json
            |> CachedTags.Parse
            |> Ok
        with
        | e -> Error (ParseError e.Message)

// TODO: Submodule of Tags?
module Exclusions =
    open Utilities
    open Settings
    open Tags

    let private excludeFile (settings: SettingsType) (file: CachedTags.Root) =
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
                 tags.AlbumArtists |> contains a || tags.Artists |> contains a
             | None, Some t ->
                 tags.Title.StartsWith(t, StringComparison.InvariantCultureIgnoreCase)
             | _ -> false

        settings.Exclusions
        |> Array.exists isExcluded

    let filterTags (settings: SettingsType) (allTags: CachedTags.Root array) =
        allTags
        |> Array.filter (fun x -> not <| excludeFile settings x)

module Operators =
    let (>>=) result func = Result.bind func result
    let (<!>) result func = Result.map func result
    let (<.>) result func = Result.tee func result

open Errors
open Utilities
open Settings
open Tags
open Exclusions
open Operators

let printTagCount (isFiltered: bool) (tags: CachedTags.Root array) =
    match isFiltered with
    | false -> $"Total file count:    %s{formatNumber tags.Length}"
    | true -> $"Filtered file count: %s{formatNumber tags.Length}"
    |> printfn "%s"

let run () =
    result {
        let! settings =
            Settings.load ()
            <.> summarize

        return
            ArgValidation.validateFilePath ()
            >>= IO.readFile
            >>= IO.parseJson
            <.> printTagCount false
            <!> filterTags settings
            <.> printTagCount true
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
