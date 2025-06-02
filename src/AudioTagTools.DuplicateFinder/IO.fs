module IO

open System.IO
open Errors
open Operators

let readFile (fileInfo: FileInfo) : Result<string, Error> =
    try
        fileInfo.FullName
        |> File.ReadAllText
        |> Ok
    with
    | ex -> Error (IoError ex.Message)
