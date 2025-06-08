module Errors

type Error =
    | InvalidArgCount
    | IoError of string // TODO: Split into read and write errors.
    | TagParseError of string

let message = function
    | InvalidArgCount -> "Invalid arguments. Pass in (1) your tag library path and (2) the desired path for your exported genres file."
    | IoError msg -> $"I/O failure: {msg}"
    | TagParseError msg -> $"Unable to parse the tag library file: {msg}"
