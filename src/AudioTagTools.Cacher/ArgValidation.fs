module ArgValidation

open Errors
open System.IO

let validate (args: string array) : Result<DirectoryInfo * FileInfo, Error> =
    match args with
    | [| a; b |] -> Ok (DirectoryInfo a, FileInfo b)
    | _ -> Error InvalidArgCount
