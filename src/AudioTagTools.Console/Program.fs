let commandMap =
    [ "cache-tags", AudioTagTools.Cacher.start
      "find-duplicates", AudioTagTools.DuplicateFinder.start
      "export-genres", AudioTagTools.GenreExtractor.start ]
    |> Map.ofList

let commandInstructions =
    commandMap
    |> Map.keys
    |> String.concat "\" or \""
    |> sprintf "You must pass in a supported command: \"%s\"."

type ExitCode =
    | Success = 0
    | Failure = 1

[<EntryPoint>]
let main args : int =
    let watch = Startwatch.Library.Watch()

    match args with
    | [| |] ->
        printfn $"{commandInstructions}"
        ExitCode.Failure
    | _ ->
        let command = args[0]
        let flags = args[1..]

        printfn "Starting..."

        match Map.tryFind command commandMap with
        | Some requestedOperation ->
            match requestedOperation flags with
            | Ok msg ->
                printfn $"{msg}"
                printfn $"Done in {watch.ElapsedFriendly}."
                ExitCode.Success
            | Error msg ->
                printfn $"{msg}"
                printfn $"Failed after {watch.ElapsedFriendly}."
                ExitCode.Failure
        | None ->
            printfn $"Invalid command \"{command}\". {commandInstructions}"
            ExitCode.Failure
    |> int
