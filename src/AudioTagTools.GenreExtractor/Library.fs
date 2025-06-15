module AudioTagTools.GenreExtractor

open Operators
open Errors
open Exporting
open ArgValidation
open Utilities
open Shared.IO
open FsToolkit.ErrorHandling

let private run (args: string array) : Result<unit, Error> =
    result {
        let! tagLibraryFile, genreFile = validate args

        let! output =
            tagLibraryFile
            |> IO.readFile
            >>= IO.parseToTags
            <.> fun ts -> printfn $"Parsed tags for {formatInt ts.Length} files from the tag library."
            <!> getArtistsWithGenres
            <.> fun x -> printfn $"Prepared {formatInt x.Length} artist-genre pairs."

        let! _ =
            copyToBackupFile genreFile
            |> Result.mapError (fun x -> IoWriteError x.Message)

        return! IO.writeLines genreFile.FullName output
    }

let start args : Result<string, string> =
    match run args with
    | Ok _ -> Ok "Finished exporting genres successfully!"
    | Error e -> Error (message e)
