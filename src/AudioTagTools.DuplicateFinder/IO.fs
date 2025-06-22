module IO

open Errors
open System.IO
open AudioTagTools.Shared.IO

let readFile (fileInfo: FileInfo) : Result<string, Error> =
    readFile fileInfo
    |> Result.mapError (fun ex -> ReadFileError ex.Message)
