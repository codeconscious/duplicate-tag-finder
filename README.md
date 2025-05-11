# Cached Tag Creater and Duplicate Finder

This is a pair of F# scripts that do two things:
- Cache the audio metadata (i.e., tags) in audio files from a specified directory to a specified file on your computer
- Parse the cached data and reports on likely duplicates

I mainly created these to practice with [JSON type providers](https://fsprojects.github.io/FSharp.Data/library/JsonProvider.html) in F#.

# Requirements

- [.NET 9 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- Duplicate search only: a JSON settings file

# Running

## Caching tags

Pass two arguments to TagCacher.fsx:
1. The directory containing your audio file
2. The file that contains (or will contain) your cached audio tags (It need not exist yet)


```sh
dotnet fsi TagCacher.fsx "/Users/me/Documents/Audio" "/Users/me/Documents/Audio/tagCache.json"
```

If the specified file already exists, it will automatically be backed up in the same directory.

## Finding duplicates

First, you must already have a JSON file that contains the tag data that you wish to examine, and it must in this format:

```json
[
  {
    "FileNameOnly": "FILENAME.m4a",
    "DirectoryName": "FULL_DIRECTORY_NAME",
    "Artists": [
      "SAMPLE ARTIST NAME"
    ],
    "AlbumArtists": [],
    "Album": "ALBUM_NAME",
    "TrackNo": 0,
    "Title": "TRACK_TITLE",
    "Year": 1950,
    "Genres": [
      "GENRE1"
    ],
    "Duration": "00:03:39.6610000",
    "LastWriteTime": "2024-09-12T09:40:54+09:00"
  }
]
```

(This is the same format that the `--cache-tags` option of [my AudioTagger utility](https://github.com/codeconscious/audiotagger) outputs.)

Second, you must have a settings file in the script directory. I have provided a sample below.

```json
{
  "cachedTagFile": "YOUR_FULL_PATH_HERE.json",
  "exclusions": [
    {
      "artist": "SAMPLE_ARTIST_NAME",
      "title": "SAMPLE_TRACK_NAME"
    },
    {
      "artist": "SAMPLE_ARTIST_NAME"
    },
    {
      "title": "SAMPLE_TRACK_NAME"
    },
  ],
  "artistReplacements": [
    " ",
    "　",
    "The ",
    "ザ・"
  ],
  "titleReplacements": [
    " ",
    "　",
    "(",
    ")",
    "（",
    "）",
    "[",
    "]",
    "'",
    "’",
    "\"",
    "”",
    "-",
    "–",
    "—",
    "~",
    "〜",
    "/",
    "／",
    "|",
    "｜",
    "?",
    "？",
    "!",
    "！",
    "~",
    "〜",
    "～",
    "=",
    "＝",
    "&",
    "＆",
    "#",
    "＃",
    "•",
    "・",
    ".",
    "。",
    ",",
    "、",
    ":",
    "：",
    "...",
    "…",
    "*",
    "＊",
    "+",
    "＋",
    "=",
    "＝",
    "✖︎",
    "❌",
  ]
}
```

After everything is set up, just run the following command:

```
dotnet fsi DuplicateTagFinder.fsx
```

If any duplicates are found, they will be listed in groups. Otherwise, nothing is listed. (I will improve this later.)
