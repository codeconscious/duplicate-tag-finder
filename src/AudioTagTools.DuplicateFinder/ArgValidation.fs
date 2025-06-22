module ArgValidation

open Errors
open System.IO

let validate (args: string array) : Result<FileInfo * FileInfo, Error> =
    match args with
    | [| x; y |] -> Ok (FileInfo x, FileInfo y)
    | _ -> Error InvalidArgCount
