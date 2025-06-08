module AudioTagTools.GenreExtractor

open System
open Operators
open Errors
open Exporting
open ArgValidation
open IO
open TagLibrary
open AudioTagTools.Shared.IO
open FsToolkit.ErrorHandling

let private run (args: string array) : Result<unit, Error> =
    result {
        let! tagLibraryFile, genreFile = validate args

        let! output =
            tagLibraryFile
            |> IO.readFile
            >>= IO.parseToTags
            <.> fun ts -> printfn $"Parsed tags for {ts.Length} files from the tag library."
            <!> getArtistsWithGenres

        let! _ = copyToBackupFile genreFile |> Result.mapError (fun x -> IoError x.Message)
        return! IO.writeFile genreFile.FullName output
    }

let start args : Result<string, string> =
    match run args with
    | Ok _ -> Ok "Finished exporting genres successfully!"
    | Error e -> Error (message e)
