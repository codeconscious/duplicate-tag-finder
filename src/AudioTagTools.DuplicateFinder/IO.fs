module IO

open System.IO
open Errors
open AudioTagTools.Shared.IO

let readFile (fileInfo: FileInfo) : Result<string, Error> =
    readFile fileInfo
    |> Result.mapError (fun ex -> IoError ex.Message)
