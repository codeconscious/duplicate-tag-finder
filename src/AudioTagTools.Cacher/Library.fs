module AudioTagTools.Cacher

open System
open Errors
open IO
open Tags
open FsToolkit.ErrorHandling

let run (args: string array) =
    result {
        let! mediaDir, tagCacheFile = ArgValidation.validate args
        let! fileInfos = getFileInfos mediaDir
        let! cachedTagMap = createCachedTagMap tagCacheFile
        let! newJson = generateNewJson cachedTagMap fileInfos

        let! _ = copyToBackupFile tagCacheFile
        do! writeFile tagCacheFile.FullName newJson
    }

let start args =
    match run args with
    | Ok _ -> Ok "Finished caching successfully!"
    | Error e -> Error (message e)
