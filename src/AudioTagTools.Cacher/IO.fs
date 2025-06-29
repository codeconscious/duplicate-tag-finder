module IO

open System.IO
open Errors
open Utilities

type TaggedFile = TagLib.File

let readfile filePath : Result<string, Error> =
    match readAllText filePath with
    | Ok x -> Ok x
    | Error msg -> Error (ReadFileError msg.Message)

let getFileInfos (dirPath: DirectoryInfo) : Result<FileInfo seq, Error> =
    let isSupportedAudioFile (fileInfo: FileInfo) =
        [".mp3"; ".m4a"; ".mp4"; ".ogg"; ".flac"]
        |> List.contains fileInfo.Extension

    try
        dirPath.EnumerateFiles("*", SearchOption.AllDirectories)
        |> Seq.filter isSupportedAudioFile
        |> Ok
    with
    | e -> Error (GeneralIoError e.Message)

let readFileTags (filePath: string) : Result<TaggedFile, Error> =
    try Ok (TaggedFile.Create filePath)
    with e -> Error (ParseError e.Message)

let writeFile (filePath: string) (content: string) : Result<unit, Error> =
    try Ok (File.WriteAllText(filePath, content))
    with e -> Error (WriteFileError e.Message)
