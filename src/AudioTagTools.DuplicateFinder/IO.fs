module IO

open System
open System.Text
open Errors
open Settings
open System.IO
open AudioTagTools.Shared.IO
open FsToolkit.ErrorHandling

let readFile (fileInfo: FileInfo) : Result<string, Error> =
    readFile fileInfo
    |> Result.mapError ReadFileError

let savePlaylist
    (settings: SettingsRoot)
    (tags: Map<string, TagLibrary.FilteredTagCollection>)
    : Result<string, Error>
    =
    let mutable contents = StringBuilder("#EXTM3U\n")
    let now = DateTime.Now.ToString("yyyyMMdd_HHmmss")
    let filename = $"Duplicates by AudioTagTools - {now}.m3u"
    let fullPath = Path.Combine(settings.Playlist.SaveDirectory, filename)

    let combine (x: string array) (y: string array) =
        let xy = Array.append x y
        String.Join("; ", xy)

    let update (m: TagLibrary.FileTags) : unit =
        let seconds = m.Duration.TotalSeconds
        let artist = combine m.AlbumArtists m.Artists
        let artistTitle = $"{artist} - {m.Title}"
        let extInf = $"#EXTINF:{seconds},{artistTitle}"
        contents.AppendLine extInf |> ignore

        let fullPath = Path.Combine(m.DirectoryName, m.FileNameOnly)
        let searchFor, replaceWith = settings.Playlist.SearchPath, settings.Playlist.ReplacePath

        let updatedPath =
            match searchFor, replaceWith with
            | s, _ when String.IsNullOrEmpty s -> fullPath
            | s, r -> fullPath.Replace(s, r)

        contents.AppendLine updatedPath |> ignore

    tags.Values
    |> Seq.collect id
    |> Seq.iter update

    writeTextToFile fullPath (contents.ToString())
    |> Result.map (fun _ -> fullPath)
    |> Result.mapError WriteFileError
