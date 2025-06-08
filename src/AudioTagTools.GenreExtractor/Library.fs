module AudioTagTools.GenreExtractor

open System
open Operators
open Errors
open Exporting
open ArgValidation
open IO
open FsToolkit.ErrorHandling

let private run (args: string array) : Result<unit, Error> =
    result {
        let! tagLibraryFile, genreFile = validate args

        return!
            tagLibraryFile
            |> readFile
            >>= parseToTags
            <!> processFiles
            >>= writeFile genreFile.FullName
    }

let start args : Result<string, string> =
    match run args with
    | Ok _ -> Ok "Finished exporting genres successfully!"
    | Error e -> Error (message e)
