let commandMap =
    [
        "update-cache", AudioTagTools.Cacher.start
        "find-duplicates", AudioTagTools.DuplicateFinder.start
    ]
    |> Map.ofList

[<EntryPoint>]
let main args =
    let watch = Startwatch.Library.Watch()

    match args.Length with
    | l when l = 0 ->
        printfn "You must pass in a supported command."
        1
    | _ ->
        let command = args[0]

        commandMap
        |> Map.tryFind command
        |> function
            | Some fn ->
                match fn (args |> Array.tail) with
                | Ok msg ->
                    printfn $"{msg}"
                    printfn $"Done in {watch.ElapsedFriendly}."
                    0
                | Error msg ->
                    printfn $"{msg}"
                    printfn $"Failed after {watch.ElapsedFriendly}."
                    1
            | None ->
                printfn $"Invalid command \"{command}\". You must pass in a supported command."
                1
