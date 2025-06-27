module IO

open Errors
open Settings
open TagLibrary
open AudioTagTools.Shared.IO
open System
open System.Text
open System.IO
open FsToolkit.ErrorHandling

let readFile (fileInfo: FileInfo) : Result<string, Error> =
    fileInfo
    |> readFile
    |> Result.mapError ReadFileError

let savePlaylist (settings: SettingsRoot) (tags: FileTags array array) : Result<unit, Error> =
    let now = DateTime.Now.ToString("yyyyMMdd_HHmmss")
    let filename = $"Duplicates by AudioTagTools - {now}.m3u"
    let fullPath = Path.Combine(settings.Playlist.SaveDirectory, filename)

    let appendFileEntry (builder: StringBuilder) (m: FileTags) : StringBuilder =
        let seconds = m.Duration.TotalSeconds
        let artist = Array.append m.AlbumArtists m.Artists |> String.concat "; "
        let artistWithTitle = $"{artist} - {m.Title}"
        let extInf = $"#EXTINF:{seconds},{artistWithTitle}"
        builder.AppendLine extInf |> ignore

        let fullPath = Path.Combine(m.DirectoryName, m.FileNameOnly)

        let updatedPath =
            match settings.Playlist.SearchPath, settings.Playlist.ReplacePath with
            | s, _ when String.IsNullOrEmpty s -> fullPath
            | s, r -> fullPath.Replace(s, r)

        builder.AppendLine updatedPath |> ignore
        builder

    tags
    |> Seq.collect id
    |> Seq.fold appendFileEntry (StringBuilder("#EXTM3U\n"))
    |> _.ToString()
    |> writeTextToFile fullPath
    |> Result.tee (fun _ -> printfn $"Created playlist file \"{fullPath}\".")
    |> Result.mapError WriteFileError
