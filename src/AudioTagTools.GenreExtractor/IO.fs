module IO

open System.IO
open Errors
open TagLibrary
open AudioTagTools.Shared.IO

let readFile (fileInfo: FileInfo) : Result<string, Error> =
    readFile fileInfo
    |> Result.mapError (fun ex -> IoError ex.Message)

let parseToTags (json: string) : Result<FileTagCollection, Error> =
    parseToTags json
    |> Result.mapError (fun ex -> TagParseError ex.Message)

let writeFile (filePath: string) (text: string) : Result<unit, Error> =
    writeFile filePath text
    |> Result.mapError (fun ex -> IoError ex.Message)
