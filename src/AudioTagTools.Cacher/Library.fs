module AudioTagTools.Cacher

open System
open Errors
open IO
open Tags
open ArgValidation
open FsToolkit.ErrorHandling
open AudioTagTools.Shared.IO

let private run (args: string array) : Result<unit, Error> =
    printfn "Starting..."

    result {
        let! mediaDir, tagLibraryFile = validate args
        let! fileInfos = getFileInfos mediaDir
        let! tagLibraryMap = createTagLibraryMap tagLibraryFile
        let! newJson = generateNewJson tagLibraryMap fileInfos

        let! _ =
            copyToBackupFile tagLibraryFile
            |> Result.mapError WriteFileError

        do!
            writeTextToFile tagLibraryFile.FullName newJson
            |> Result.mapError WriteFileError
    }

let start (args: string array) : Result<string, string> =
    match run args with
    | Ok _ -> Ok "Finished caching successfully!"
    | Error e -> Error (message e)
