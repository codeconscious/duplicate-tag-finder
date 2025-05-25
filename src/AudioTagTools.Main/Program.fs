open System

let commandMap =
    [
        "update-cache", AudioTagTools.Cacher.start
        "find-duplicates", AudioTagTools.DuplicateFinder.start
    ]
    |> Map.ofList

[<EntryPoint>]
let main args =
    let watch = Startwatch.Library.Watch()

    match args with
    | [| |] ->
        let commands = commandMap |> Map.keys |> (fun x -> String.Join("\" or \"", x))
        printfn $"You must pass in a supported command: \"{commands}\"."
        1
    | _ ->
        let command = args[0]
        let flags = args[1..]

        commandMap
        |> Map.tryFind command
        |> function
            | Some fn ->
                match fn flags with
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
