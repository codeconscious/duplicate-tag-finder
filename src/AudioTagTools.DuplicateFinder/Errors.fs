module Errors

type Error =
    | InvalidArgCount
    | IoError of string
    | SettingsParseError of string
    | TagParseError of string

let message = function
    | InvalidArgCount -> "Invalid arguments. Pass in two JSON filename paths: (1) your settings file and (2) your cached tag data."
    | IoError msg -> $"I/O failure: {msg}"
    | SettingsParseError msg -> $"Unable to parse the settings file: {msg}"
    | TagParseError msg -> $"Unable to parse the tag file: {msg}"
