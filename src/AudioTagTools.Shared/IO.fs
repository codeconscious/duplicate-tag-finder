module AudioTagTools.Shared.IO

open System
open System.IO

let readFile (fileInfo: FileInfo) : Result<string, string> =
    try
        fileInfo.FullName
        |> File.ReadAllText
        |> Ok
    with ex -> Error ex.Message

let writeTextToFile (filePath: string) (text: string) : Result<unit, string> =
    try Ok (File.WriteAllText(filePath, text))
    with ex -> Error ex.Message

let writeLinesToFile (filePath: string) (lines: string array) : Result<unit, string> =
    try Ok (File.WriteAllLines(filePath, lines))
    with ex -> Error ex.Message

let copyToBackupFile (fileInfo: FileInfo) : Result<FileInfo option, string> =
    let generateBackUpFilePath (tagLibraryFile: FileInfo) : string =
        let baseName = Path.GetFileNameWithoutExtension tagLibraryFile.Name
        let timestamp = DateTimeOffset.Now.ToString "yyyyMMdd_HHmmss"
        let extension = tagLibraryFile.Extension // Includes the initial period.
        let fileName = $"%s{baseName}.%s{timestamp}_backup%s{extension}"
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
        | ex -> Error ex.Message
    else Ok None
