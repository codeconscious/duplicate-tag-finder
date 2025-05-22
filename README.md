# Tag Cacher and Duplicate Finder

This is a pair of F# scripts that each do one task:
1. **TagCacher** caches the audio metadata tags in supported audio files within a specified directory to a file on your computer
2. **DuplicateTagFinder** parses those cached tags and reports likely duplicates, based on the provided settings

I mainly created these to practice with F# [JSON type providers](https://fsprojects.github.io/FSharp.Data/library/JsonProvider.html), but they do resolve a small pain point for me as well.

# Requirements

- [.NET 9 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- JSON settings file (DuplicateTagFinder only)

# Running

## TagCacher

Pass two arguments to TagCacher.fsx:
1. The directory containing your audio files
2. The file that contains (or will contain, if it does not exist yet) your cached audio tags

Run the script like this:

```sh
dotnet fsi TagCacher.fsx "/Users/me/Documents/Audio" "/Users/me/Documents/Audio/tagCache.json"
```

If the specified tag file already exists, it will automatically be backed up in the same directory before a new one is created.

## DuplicateTagFinder

First, you must have a settings JSON file that contains the cached tag data to search through, and it must in this format:

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

Second, you must have a settings file prepared as well. I have provided a sample below.

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

Pass the settings file and tag-cached file to the script (in that order) like this:

```sh
dotnet fsi DuplicateTagFinder.fsx "/Users/me/Documents/duplicate-finder-settings.json" "/Users/me/Downloads/Music/library.json"
```

If any files that appear to be duplicates are found, they will be listed in groups. If you see false positives, you can add the artist and/or title to the exclusions in your settings to hide them.
