module IO

open System.IO
open Errors
open Operators

let readFile (fileName: FileInfo) : Result<string, Error> =
    try
        fileName.FullName
        |> File.ReadAllText
        |> Ok
    with
    | ex -> Error (IoError ex.Message)
