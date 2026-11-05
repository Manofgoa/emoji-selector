<img src="https://flagcdn.com/w20/gb.png" width="20" alt="GB"> English · [<img src="https://flagcdn.com/w20/fr.png" width="20" alt="FR"> Français](CONTRIBUTING.fr.md)

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
| `subgroups.json` | Derived from `en/data.json`, `en/messages.json` and `fr/messages.json` | The sub-categories: the subgroups' names, each emoji's subgroup |

`version.txt` next to them holds the version, `LICENSE` and it embedded too. At launch,
`Data/EmojiDataFolder` writes this embedded copy to `emoji-data\` next to the exe when that folder is
missing, damaged or older, and reads the folder; `Data/EmojiCatalog` builds the categories. The
app's *Check for emoji updates…* replaces the folder with a newer published version
(`Data/EmojiDataUpdate`).

To update the embedded copy, replace both files and `LICENSE` with those of the new
`emojibase-data` version (`https://cdn.jsdelivr.net/npm/emojibase-data@<version>/<en|fr>/compact.json`),
then update `version.txt` and the version above, and generate `subgroups.json` again — the script
reads `version.txt` and downloads nothing else:

```powershell
pwsh src/EmojiSelector/Data/Emojibase/New-Subgroups.ps1
```

The app's *Check for emoji updates…* derives the same file for a newer version
(`Data/EmojiSubgroups`).

## Special characters

The special characters' **default list** — five families, the sub-categories of the special
characters' tab, each character with its English and French names and its aliases — is `src/EmojiSelector/Data/SpecialCharacters.json`, embedded in the exe. At
launch, `Data/SpecialCharacters` writes it to `special-characters.json` next to the exe **only when
that file is missing**: a file already there is the user's and is never overwritten. Changing the
default therefore reaches new installs and users who reset the app, not the others.

The file is camelCase JSON, UTF-8 without BOM, the characters written as themselves:
`{ "families": [ { "name", "characters": [ { "character", "name", "frenchName", "aliases" } ] } ] }`.
The English name is the Unicode name in lower case (`copyright sign`), the French one the usual
French name (`symbole copyright`).

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

## Flag images

Windows' emoji font has no country flags. The country flags and the subdivision flags (England,
Scotland, Wales) are drawn from [Twemoji](https://github.com/jdecked/twemoji)'s SVGs, **version
17.0.3** — graphics by Twitter, Inc and other contributors, licensed under
[CC-BY 4.0](https://creativecommons.org/licenses/by/4.0/) (see
[its LICENSE-GRAPHICS](src/EmojiSelector/Drawing/Twemoji/LICENSE-GRAPHICS)), kept as published, unchanged,
in `src/EmojiSelector/Drawing/Twemoji/` and embedded in the exe:

| File | Is |
|---|---|
| `<code points>.svg` | One flag, `assets/svg/` of the Twemoji repository: `1f1eb-1f1f7.svg` for 🇫🇷 |
| `LICENSE-GRAPHICS` | Twemoji's graphics license |
| `version.txt` | The Twemoji version |
| `Update-FlagImages.ps1` | The script downloading them |

Only the flags the embedded emoji data needs and Windows' font cannot draw are there
(`Drawing/FlagImages.NeedsImage`); the other flags (🏁, 🏳️‍🌈…) are the font's. The files are committed:
the build downloads nothing. After updating the embedded emoji data (see *Emoji data*) or to move to
another Twemoji version, run the script — it downloads from jsDelivr, a failure leaves the files as
they were — then update the version above:

```powershell
pwsh src/EmojiSelector/Drawing/Twemoji/Update-FlagImages.ps1 -Version <version>
```

At run time, *Check for emoji updates…* downloads the flag images a later Twemoji version changed or
added to `flag-images\` next to the exe (`Data/FlagImagesUpdate`), read on top of the embedded ones.

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
