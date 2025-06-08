module Exporting

open Errors
open TagLibraryIo

let parseToTags json =
    parseToTags json
    |> Result.mapError (fun ex -> TagParseError ex.Message)
