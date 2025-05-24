module IO

open System.IO
open Errors
open Operators

let readFile (fileName: string) : Result<string, Error> =
    try
        fileName
        |> File.ReadAllText
        |> Ok
    with
    | ex -> Error (IoError ex.Message)
