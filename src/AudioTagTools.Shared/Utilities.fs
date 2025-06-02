module Utilities

open System
open System.Text.Json
open System.Text.Encodings.Web
open System.Text.Unicode
open System.Globalization

/// Serialize items to formatted JSON, returning a Result.
/// If an exception is thrown during the underlying operation,
/// the Error only includes its message.
let serializeToJson items : Result<string, string> =
    try
        let serializerOptions = JsonSerializerOptions()
        serializerOptions.WriteIndented <- true
        serializerOptions.Encoder <- JavaScriptEncoder.Create UnicodeRanges.All
        JsonSerializer.Serialize(items, serializerOptions)
        |> Ok
    with
    | ex -> Error ex.Message

/// Converts an int to friendly string format. Sample: 1000 -> "1,000".
let formatNumber (i: int) : string =
    i.ToString("N0", CultureInfo.InvariantCulture)

/// Removes all instances of multiple substrings from a given string.
let removeSubstrings (substrings: string array) (text: string) : string =
    Array.fold
        (fun acc x -> acc.Replace(x, String.Empty))
        text
        substrings

/// Confirms whether the text of a string exists in any element of nested collections.
let anyContains (collections: string seq seq) (target: string) : bool =
    collections
    |> Seq.concat
    |> Seq.exists (fun text -> StringComparer.InvariantCultureIgnoreCase.Equals(text, target))

/// Reads all text from the specified file, returning a Result.
/// If an exception is thrown during the underlying operation,
/// the Error includes the exception itself.
let readAllText (filePath: string) : Result<string, exn> =
    try
        filePath
        |> System.IO.File.ReadAllText
        |> Ok
    with
    | ex -> Error ex
