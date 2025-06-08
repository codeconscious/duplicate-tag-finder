module AudioTagTools.Shared.IO

open System
open System.IO

let readFile (fileInfo: FileInfo) : Result<string, exn> =
    try
        fileInfo.FullName
        |> File.ReadAllText
        |> Ok
    with ex -> Error ex

let writeFile (filePath: string) (text: string) : Result<unit, exn> =
    try Ok (File.WriteAllText(filePath, text))
    with ex -> Error ex

let copyToBackupFile (fileInfo: FileInfo) : Result<FileInfo option, exn> =
    let generateBackUpFilePath (tagLibraryFile: FileInfo) : string =
        let baseName = Path.GetFileNameWithoutExtension tagLibraryFile.Name
        let timestamp = DateTimeOffset.Now.ToString "yyyyMMdd_HHmmss"
        let extension = tagLibraryFile.Extension // Includes the initial period.
        let fileName = sprintf "%s-%s%s" baseName timestamp extension
        Path.Combine(tagLibraryFile.DirectoryName, fileName)

    let printConfirmation (backupFile: FileInfo) =
        printfn "Backed up previous file to \"%s\"." backupFile.Name
        backupFile

    if fileInfo.Exists
    then
        try
            fileInfo
            |> generateBackUpFilePath
            |> fileInfo.CopyTo
            |> printConfirmation
            |> Some
            |> Ok
        with
        | ex -> Error ex
    else Ok None

