module IO

open System.IO
open Errors
open TagLibraryIo

let readFile (fileInfo: FileInfo) : Result<string, Error> =
    readFile fileInfo
    |> Result.mapError (fun ex -> IoError ex.Message)
