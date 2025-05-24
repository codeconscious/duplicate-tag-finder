module Errors

type Error =
    | InvalidArgCount
    | MediaDirectoryMissing of string
    | ReadFileError of string
    | WriteFileError of string
    | IoError of string
    | ParseError of string
    | JsonSerializationError of string

let message = function
    | InvalidArgCount -> "Invalid arguments. Pass in (1) the directory containing your audio files and (2) a path to a JSON file containing cached tag data."
    | MediaDirectoryMissing msg -> $"Directory \"{msg}\" was not found."
    | ReadFileError msg -> $"Read failure: {msg}"
    | WriteFileError msg -> $"Write failure: {msg}"
    | IoError msg -> $"I/O failure: {msg}"
    | ParseError msg -> $"Parse error: {msg}"
    | JsonSerializationError msg -> $"JSON serialization error: {msg}"
