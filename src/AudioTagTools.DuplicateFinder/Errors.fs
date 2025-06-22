module Errors

type Error =
    | InvalidArgCount
    | ReadFileError of string
    | SettingsParseError of string
    | TagParseError of string

let message = function
    | InvalidArgCount -> "Invalid arguments. Pass in two JSON file paths: (1) your settings file and (2) your tag library."
    | ReadFileError msg -> $"Read failure: {msg}"
    | SettingsParseError msg -> $"Unable to parse the settings file: {msg}"
    | TagParseError msg -> $"Unable to parse the tag library file: {msg}"
