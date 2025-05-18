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

    let message: Error -> string = function
        | InvalidArgCount -> "Invalid arguments. Pass in the path to the JSON file containing your cached tag data."
        | FileMissing fileName -> $"The file \"{fileName}\" was not found."
        | IoError msg -> $"I/O failure: {msg}"
        | ParseError msg -> $"Could not parse the content: {msg}"

module Utilities =
    let extractText (x: Runtime.BaseTypes.IJsonDocument) : string =
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

    let validateArgCount (args: string array) : Result<string, Error> =
        if args.Length <> 2 // Index 0 is the name of the script itself.
        then Error InvalidArgCount
        else Ok args[1]

module Settings =
    open Errors

    [<Literal>]
    let settingsPath = "settings.json"

    type SettingsProvider = JsonProvider<settingsPath>
    type SettingsRoot = SettingsProvider.Root

    type ExclusionPair =
        { Artist: string option
          Title: string option }

    type SettingsType =
        { Exclusions: ExclusionPair array
          ArtistReplacements: string array
          TitleReplacements: string array }

    let toSettings (settings: SettingsRoot) : SettingsType =
        { Exclusions =
              settings.Exclusions
              |> Array.map (fun e -> { Artist = e.Artist; Title = e.Title })
          ArtistReplacements = settings.ArtistReplacements
          TitleReplacements = settings.TitleReplacements }

    let load () : Result<SettingsType,Error> =
        try
            Ok (SettingsProvider.Load settingsPath |> toSettings)
        with
        | e -> Error (IoError e.Message)

    let printSummary (settings: SettingsType) =
        printfn $"Exclusions:          %d{settings.Exclusions.Length}"
        printfn $"Artist Replacements: %d{settings.ArtistReplacements.Length}"
        printfn $"Title Replacements:  %d{settings.TitleReplacements.Length}"

module Tags =
    open Errors
    open Utilities
    open Settings

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

    type TagJsonProvider = JsonProvider<tagSample>
    type FileTags = TagJsonProvider.Root
    type TagCollection = FileTags array
    type FilteredTagCollection = FileTags array

    let confirmFileExists (fileName: string) : Result<FileInfo,Error> =
        let fileInfo = FileInfo fileName
        if fileInfo.Exists
        then Ok fileInfo
        else Error (FileMissing fileInfo.FullName)

    let readFile (fileInfo: FileInfo) : Result<string, Error> =
        try
            fileInfo.FullName
            |> System.IO.File.ReadAllText
            |> Ok
        with
        | e -> Error (IoError e.Message)

    let parseJsonToTags (json: string) : Result<TagCollection, Error> =
        try
            json
            |> TagJsonProvider.Parse
            |> Ok
        with
        | e -> Error (ParseError e.Message)

    let filter (settings: SettingsType) (allTags: TagCollection) : FileTags array =
        let excludeFile (settings: SettingsType) (file: FileTags) =
            let contains (target: string) (collection: string seq) =
                collection
                |> Seq.exists (fun x -> StringComparer.InvariantCultureIgnoreCase.Equals(x, target))

            let tags =
                {|
                    AlbumArtists = Seq.map extractText file.AlbumArtists
                    Artists = Seq.map extractText file.Artists
                    Title = extractText file.Title
                |}

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

        allTags
        |> Array.filter (fun x -> not <| excludeFile settings x)

    let findDuplicates
        (settings: SettingsType)
        (tags: FilteredTagCollection)
        : (string * FilteredTagCollection) array
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

    let printTotalCount (tags: TagCollection) =
        printfn $"Total file count:    %s{formatNumber tags.Length}"

    let printFilteredCount (tags: FilteredTagCollection) =
        printfn $"Filtered file count: %s{formatNumber tags.Length}"

    let printResults (groupedTracks: (string * FilteredTagCollection) array) =
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

module Operators =
    let (>>=) result func = Result.bind func result
    let (<!>) result func = Result.map func result
    let (<.>) result func = Result.tee func result

open ArgValidation
open Errors
open Tags
open Settings
open Operators

let run () =
    result {
        let! settings = Settings.load () <.> printSummary

        return
            fsi.CommandLineArgs
            |> validateArgCount
            >>= confirmFileExists
            >>= readFile
            >>= parseJsonToTags
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
