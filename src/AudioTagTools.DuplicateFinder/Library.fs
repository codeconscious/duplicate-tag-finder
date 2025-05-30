module AudioTagTools.DuplicateFinder

open System
open Shared
open Errors
open ArgValidation
open Operators
open IO
open Tags
open Settings
open FsToolkit.ErrorHandling

let run (args: string array) : Result<unit, Error> =
    result {
        let! settingsFile, cachedTagFile = validate args

        let! settings =
            settingsFile
            |> readFile
            >>= parseToSettings
            <.> printSummary

        return
            cachedTagFile
            |> readFile
            >>= parseToTags
            <.> printTotalCount
            <!> filter settings
            <.> printFilteredCount
            <!> findDuplicates settings
            <&> printResults
    }

let start args =
    match run args with
    | Ok _ -> Ok "Finished searching successfully!"
    | Error e -> Error (message e)
