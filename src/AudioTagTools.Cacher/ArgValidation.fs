module ArgValidation

open Errors
open System.IO

let validate (args: string array) : Result<DirectoryInfo * FileInfo, Error> =
    if args.Length = 2
    then Ok (DirectoryInfo args[0], FileInfo args[1])
    else Error InvalidArgCount
