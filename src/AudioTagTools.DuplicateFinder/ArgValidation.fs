module ArgValidation

open System.IO
open Errors

let validate (args: string array) : Result<FileInfo * FileInfo, Error> =
    match args with
    | [| a; b |] -> Ok (FileInfo a, FileInfo b)
    | _ -> Error InvalidArgCount


