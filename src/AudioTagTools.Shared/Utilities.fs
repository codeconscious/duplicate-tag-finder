module Utilities

open System
open System.Text.Json
open System.Text.Encodings.Web
open System.Text.Unicode
open System.Globalization

let serializeToJson items : Result<string, string> =
    try
        let serializerOptions = JsonSerializerOptions()
        serializerOptions.WriteIndented <- true
        serializerOptions.Encoder <- JavaScriptEncoder.Create UnicodeRanges.All
        JsonSerializer.Serialize(items, serializerOptions)
        |> Ok
    with
    | ex -> Error ex.Message

let formatNumber (i: int) : string =
    i.ToString("N0", CultureInfo.InvariantCulture) // Sample: 1,000

let removeSubstrings (substrings: string array) (text: string) : string =
    Array.fold
        (fun acc x -> acc.Replace(x, String.Empty))
        text
        substrings

let anyContains (collections: string seq seq) (target: string) : bool =
    collections
    |> Seq.concat
    |> Seq.exists (fun text -> StringComparer.InvariantCultureIgnoreCase.Equals(text, target))

let readAllText (fileName: string) : Result<string, exn> =
    try
        fileName
        |> System.IO.File.ReadAllText
        |> Ok
    with
    | ex -> Error ex
