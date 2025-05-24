module IO

open System
open System.IO
open Errors
open Utilities

type TaggedFile = TagLib.File

let readfile fileName : Result<string, Error> =
    match readAllText fileName with
    | Ok x -> Ok x
    | Error msg -> Error (IoError msg.Message)

let getFileInfos (dirPath: DirectoryInfo) =
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
    TagLib.File.Create filePath

let writeFile (fileName: string) (content: string) =
    try
        File.WriteAllText(fileName, content)
        |> Ok
    with
    | e -> Error (WriteFileError e.Message)

let generateBackUpFilePath (cachedTagFile: FileInfo) =
    let baseName = Path.GetFileNameWithoutExtension cachedTagFile.Name
    let timestamp = DateTimeOffset.Now.ToString "yyyyMMdd_HHmmss"
    let extension = cachedTagFile.Extension // Includes the initial period.
    let fileName = sprintf "%s-%s%s" baseName timestamp extension
    Path.Combine(cachedTagFile.DirectoryName, fileName)

let copyToBackupFile (cachedTagFile: FileInfo) =
    let printConfirmation (backupFile: FileInfo) =
        printfn "Backed up previous tag file to \"%s\"." backupFile.Name
        backupFile

    if cachedTagFile.Exists
    then
        try
            cachedTagFile
            |> generateBackUpFilePath
            |> cachedTagFile.CopyTo
            |> printConfirmation
            |> Some
            |> Ok
        with
        | e -> Error (IoError $"Could not create tag backup file: {e.Message}")
    else Ok None
