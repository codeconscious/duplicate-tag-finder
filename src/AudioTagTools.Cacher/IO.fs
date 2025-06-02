module IO

open System
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

let generateBackUpFilePath (tagLibraryFile: FileInfo) : string =
    let baseName = Path.GetFileNameWithoutExtension tagLibraryFile.Name
    let timestamp = DateTimeOffset.Now.ToString "yyyyMMdd_HHmmss"
    let extension = tagLibraryFile.Extension // Includes the initial period.
    let fileName = sprintf "%s-%s%s" baseName timestamp extension
    Path.Combine(tagLibraryFile.DirectoryName, fileName)

let copyToBackupFile (tagLibraryFile: FileInfo) : Result<FileInfo option, Error> =
    let printConfirmation (backupFile: FileInfo) =
        printfn "Backed up previous tag file to \"%s\"." backupFile.Name
        backupFile

    if tagLibraryFile.Exists
    then
        try
            tagLibraryFile
            |> generateBackUpFilePath
            |> tagLibraryFile.CopyTo
            |> printConfirmation
            |> Some
            |> Ok
        with
        | e -> Error (IoError $"Could not create tag backup file: {e.Message}")
    else Ok None
