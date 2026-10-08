# Contributing

Thanks for your interest in Emoji Selector. Issues and pull requests are welcome.

## Build

- Requires the .NET 10 SDK, on **Windows 10 version 2004 or later**.
- Run: `dotnet run --project src/EmojiSelector`
- Publish: `dotnet publish src/EmojiSelector -c Release` →
  `src/EmojiSelector/bin/Release/net10.0-windows10.0.19041.0/win-x64/publish/EmojiSelector.exe`,
  a framework-dependent single-file ReadyToRun exe that requires the .NET 10 Desktop Runtime and
  Windows 10 version 2004 or later.

There is no test project: check a change by hand in the running app.

## Emoji data

The emoji list and its keywords are [Emojibase](https://emojibase.dev)'s data, **version 17.0.0**
(MIT, see [its LICENSE](src/EmojiSelector/Data/Emojibase/LICENSE)), in two languages kept as
published in `src/EmojiSelector/Data/Emojibase/` and embedded in the exe:

| File | Source | Gives |
|---|---|---|
| `compact.en.json` | `en/compact.json` | The list: categories, order, names, English keywords |
| `compact.fr.json` | `fr/compact.json` | French keywords, joined by hexcode |

`version.txt` next to them holds the version, `LICENSE` and it embedded too. At launch,
`Data/EmojiDataFolder` writes this embedded copy to `emoji-data\` next to the exe when that folder is
missing, damaged or older, and reads the folder; `Data/EmojiCatalog` builds the categories. The
app's *Check for emoji updates…* replaces the folder with a newer published version
(`Data/EmojiDataUpdate`).

To update the embedded copy, replace both files and `LICENSE` with those of the new
`emojibase-data` version (`https://cdn.jsdelivr.net/npm/emojibase-data@<version>/<en|fr>/compact.json`),
then update `version.txt` and the version above.

## App icon

The app icon — the exe's and the window's — is the *Slightly smiling face* of Microsoft's
[Fluent Emoji](https://github.com/microsoft/fluentui-emoji), *Color* style (MIT, see
[its LICENSE](src/EmojiSelector/AppIcon/LICENSE)), kept as published in `src/EmojiSelector/AppIcon/`:

| File | Is |
|---|---|
| `slightly_smiling_face_color.svg` | The source, `assets/Slightly smiling face/Color/` of the Fluent Emoji repository |
| `app.ico` | Generated from the SVG: 16, 20, 24, 32, 40, 48, 64 and 256 px, transparent, one PNG each |
| `New-AppIcon.ps1` | The script generating `app.ico` |

`app.ico` is committed: the build does not run the script. After a change to the SVG or the sizes,
run it again — it renders the SVG with Microsoft Edge headless and downloads nothing:

```powershell
pwsh src/EmojiSelector/AppIcon/New-AppIcon.ps1
```

The tray icon is not this file: it is drawn at run time from Windows' emoji font, and shows the
emoji the user chose.

## Before changing the code

- [RULES.md](RULES.md) holds the rules every change follows. A change that breaks one of them
  updates the rule in the same pull request.
- [GLOSSARY.md](GLOSSARY.md) gives each word its one meaning: use its terms in the code, the
  comments and the documentation.
- [`workfiles/`](workfiles) keeps the design of each feature, the origin of the rules.

## Pull requests

- One feature or fix per pull request, in English: code, comments, commit messages.
- Match the surrounding code: naming, comment density, idioms.
- No new dependency: the app uses Windows' own components only, see [README § Tech](README.md#tech).
- A change the user can see updates [README.md](README.md) and [README.fr.md](README.fr.md).
