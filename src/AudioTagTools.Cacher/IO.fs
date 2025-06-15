module IO

open System.IO
open Errors
open Utilities

type TaggedFile = TagLib.File

let readfile filePath : Result<string, Error> =
    match readAllText filePath with
    | Ok x -> Ok x
    | Error msg -> Error (IoError msg.Message)

let getFileInfos (dirPath: DirectoryInfo) : Result<FileInfo seq, Error> =
    let isSupportedAudioFile (fileInfo: FileInfo) =
        [".mp3"; ".m4a"; ".mp4"; ".ogg"; ".flac"]
        |> List.contains fileInfo.Extension

    try
        dirPath.EnumerateFiles("*", SearchOption.AllDirectories)
        |> Seq.filter isSupportedAudioFile
        |> Ok
    with
    | e -> Error (IoError e.Message)

let readFileTags (filePath: string) : TaggedFile =
    TaggedFile.Create filePath

let writeFile (filePath: string) (content: string) : Result<unit, Error> =
    try
        File.WriteAllText(filePath, content)
        |> Ok
    with
    | e -> Error (WriteFileError e.Message)
