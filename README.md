# Duplicate Tag Finder

This is an F# script that parses JSON files contains audio tag data of a certain format and reports apparent duplicates. I mainly created this simple tool to practice with JSON type providers in F#.

# Requirements

- [.NET 9 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- A JSON file containing saved audio tag information
- A settings file

# Running

First, you must already have a JSON file that contains the tag data that you wish to parse. It must in this format:

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
