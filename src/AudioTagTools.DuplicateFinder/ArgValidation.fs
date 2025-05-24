module ArgValidation

open Errors

let validate (args: 'a array) : Result<'a * 'a, Error> =
    match args with
    | [| a; b |] -> Ok (a, b)
    | _ -> Error InvalidArgCount


