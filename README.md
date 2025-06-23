# Audio Tag Tools

This small command line application can do three tasks:
1. Cache metadata tags from audio files
2. Report likely duplicate files based on their tags
3. Export a list of artists with their most common genres

I originally created this tool to practice with F# [JSON type providers](https://fsprojects.github.io/FSharp.Data/library/JsonProvider.html), but it resolves a couple of small pain points for me as well.

# Requirements

- [.NET 9 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- JSON settings file (only for the duplicate search) — plus comfort manually editing such JSON files

# Running

Ensure you are in the `AudioTagTools.Main` directory in your terminal.

## Caching tags

Creates a tag library, a JSON file containing the text tag data from the audio files in a specified directory. 

Pass `cache-tags` with two arguments:

1. The path of the directory containing your audio files
2. The path of the library file that contains (or will contain, if it does not exist yet) your cached audio tags

Sample:

```sh
dotnet run -- cache-tags ~/Documents/Audio ~/Documents/Audio/tagLibrary.json
```

> [!TIP]
> The `--` is necessary to indicate that the command and arguments are for this program and not for `dotnet`.

If a library file already exists at the specified path, a backup copy will automatically be created in the same directory.

The file will be in this JSON format:

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

> [!NOTE]
> This is the same format that the `--cache-tags` option of [my AudioTagger utility](https://github.com/codeconscious/audiotagger) outputs. The advantage of using this tool instead is that it compares tag data against files' last-modified dates and only updates out-of-date tag information, making the operation considerably faster, particularly when your audio files are on a slow external drive, etc.


## Finding duplicates

First, you must already have a tag library file containing your cached tag data. See the section above if you don't have one yet.

Second, you must have a settings file containing exceptions—i.e., artists, track titles, and strings that you wish to exclude from the search. Actual entries are optional, but the file must be exist in the specified format. I have provided a sample you can use below.

<details>
  <summary>Click to expand the sample...</summary>

```json
{
  "playlist": {
    "saveDirectory": "/Users/me/Downloads/NewAudio",
    "pathSearchFor": "/Users/me/Documents/Media/",
    "pathReplaceWith": ""
  },
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

To start, use the `find-duplicates` command like this:

```sh
dotnet run -- find-duplicates ~/Documents/settings.json ~/Downloads/Music/tagLibrary.json
```

If any potential duplicates are found, they will be listed, grouped by artist. If you see false positives (i.e., tracks that were detected as duplicates, but are actually not), you can add entries to the exclusions in your settings to ignore them in the future.

## Export artist genres

Creates a text file containing a list of artists with the genre that they are most associated with in your tag library.

To use it, pass `export-genres` with two arguments:

1. The path of your library file
2. The path of the text file that contains (or will contain, if it does not exist yet) your artists with corresponding genres

Sample:

```sh
dotnet run -- export-genres ~/Downloads/Music/tagLibrary.json ~/Downloads/Music/genres.txt
```

If a genres file already exists at that path, a backup will be created automatically.
