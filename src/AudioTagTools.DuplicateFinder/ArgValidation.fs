module ArgValidation

open System.IO
open Errors

let validate (args: string array) : Result<FileInfo * FileInfo, Error> =
    match args with
    | [| x; y |] -> Ok (FileInfo x, FileInfo y)
    | _ -> Error InvalidArgCount
