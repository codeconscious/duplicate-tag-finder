module AudioTagTools.Cacher

open System
open Errors
open IO
open Tags
open ArgValidation
open FsToolkit.ErrorHandling

let private run (args: string array) : Result<unit, Error> =
    result {
        let! mediaDir, tagLibraryFile = validate args
        let! fileInfos = getFileInfos mediaDir
        let! tagLibraryMap = createTagLibraryMap tagLibraryFile
        let! newJson = generateNewJson tagLibraryMap fileInfos

        let! _ = copyToBackupFile tagLibraryFile
        do! writeFile tagLibraryFile.FullName newJson
    }

let start (args: string array) : Result<string, string> =
    match run args with
    | Ok _ -> Ok "Finished caching successfully!"
    | Error e -> Error (message e)
