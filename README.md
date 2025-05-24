# Audio Tag Tools

This small command line application can do two task:
1. Cache the audio metadata tags in supported audio files within a specified directory to a file on your computer
2. Parse those cached tags and reports likely duplicates, based on the provided settings

I mainly created it to practice with F# [JSON type providers](https://fsprojects.github.io/FSharp.Data/library/JsonProvider.html), but they do resolve a small pain point for me as well.

# Requirements

- [.NET 9 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- JSON settings file (duplicate-finder only)

# Running

## Caching tags

Pass `update-cache` with two additional arguments:
1. The path of the directory containing your audio files
2. The path of the file that contains (or will contain, if it does not exist yet) your cached audio tags

Run the script like this:

```sh
dotnet run -- update-cache "/Users/me/Documents/Audio" "/Users/me/Documents/Audio/tagCache.json"
```

If the specified tag file already exists, it will automatically be backed up in the same directory before a new one is created.

## Finding duplicates

First, you must have a settings JSON file that contains the cached tag data to search through, and it must in this format:

```json
[
  {
    "FileNameOnly": "FILENAME.m4a",
    "DirectoryName": "FULL_DIRECTORY_NAME",
    "Artists": [
      "SAMPLE ARTIST NAME"
    ],
    "AlbumArtists": [
      "SAMPLE ALBUM ARTIST NAME"
    ],
    "Album": "ALBUM_NAME",
    "TrackNo": 0,
    "Title": "TRACK_TITLE",
    "Year": 1950,
    "Genres": [
      "GENRE"
    ],
    "Duration": "00:03:39.6610000",
    "LastWriteTime": "2024-09-12T09:40:54+09:00"
  }
]
```

(This is the same format that the `--cache-tags` option of [my AudioTagger utility](https://github.com/codeconscious/audiotagger) outputs. The advantage of this version is that will comparing tag data with file dates and only update out-of-date tag information, making the operation considerably faster, particularly when your files are on an slow external drive, etc.)

Second, you must have a settings file containing items for exclusion prepared as well. I have provided a sample below.

<details>
  <summary>Click to expand!</summary>

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
</details>

Pass `find-duplicates` along with (1) the settings file path and (2) the cached-tag file path (in that order) to the script like this:

```sh
dotnet run -- find-duplicates "/Users/me/Documents/duplicate-finder-settings.json" "/Users/me/Downloads/Music/library.json"
```

If any files that appear to be duplicates are found, they will be listed in groups. If you see false positives, you can add the artist and/or title to the exclusions in your settings to hide them.
