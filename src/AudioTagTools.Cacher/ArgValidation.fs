module ArgValidation

open Errors
open System.IO

let validate (args: string array) : Result<DirectoryInfo * FileInfo, Error> =
    match args with
    | [| x; y |] -> Ok (DirectoryInfo x, FileInfo y)
    | _ -> Error InvalidArgCount
