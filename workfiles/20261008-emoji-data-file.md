# Emoji Data File

> Working document — the Emojibase data as files next to the exe, loaded at launch, recreated from the embedded copy,
> and updated from the settings menu on the user's demand.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the emoji list, its names and its keywords are Emojibase 17.0.0's `compact.json` (English and French), embedded
in the exe and read by `Data/EmojiCatalog.cs` from the manifest resources. Updating them means rebuilding the app.

The work, in two steps:

1. **Data folder** — the data lives in an `emoji-data\` folder next to the exe, read at launch. The embedded copy stays
   in the exe: it recreates the folder when it is missing, broken, or older than the exe's own data.
2. **Update check** — a settings-menu item checks, on the user's click only, whether a newer `emojibase-data` is
   published on npm, and offers to download it. The update replaces the list, the names and the keywords; the app then
   offers to restart to use it.

Components: `Data/EmojiCatalog.cs` (parsing), a new `Data/EmojiDataFolder.cs` (the folder), a new
`Data/EmojiDataUpdate.cs` (the network), `UI/MainForm.cs` (the menu item and the dialogs), the `.csproj` (one more
embedded file).

---

## Data Folder

- **`emoji-data\`** next to the exe (`AppContext.BaseDirectory`), its name in one constant
  (`EmojiDataFolder.FolderName`), kebab-case per the mini-apps rule. Not in `cache\`: that folder is disposable.
- **Its files** — a **set**, always written together:

  | File | Holds |
  |---|---|
  | `compact.en.json` | Emojibase's `en/compact.json`: the list, categories, order, names, English keywords |
  | `compact.fr.json` | Emojibase's `fr/compact.json`: French keywords, joined by hexcode |
  | `LICENSE` | Emojibase's MIT license, shipped with the data it covers |
  | `version.txt` | The Emojibase version of the set, e.g. `17.0.0` |

- **Embedded copy**: the four files of `src/EmojiSelector/Data/Emojibase/` — `version.txt` and `LICENSE` embedded too
  (`LICENSE` is not embedded today). `version.txt` replaces the version stated only in `CONTRIBUTING.md` as the
  machine-readable one.

### At launch

`EmojiDataFolder.Load()`, called where `EmojiCatalog.Load()` is today, before the catalog is built:

| The folder | Does |
|---|---|
| Missing, or a file of the set missing | The **whole set** is written from the embedded copy, then used |
| A file unreadable: a JSON that does not parse into a non-empty entry list, a `version.txt` that does not parse as a version | The whole set is rewritten from the embedded copy, then used |
| Valid, its version **older** than the embedded one (the exe was updated) | The whole set is rewritten from the embedded copy, then used |
| Valid, same version or newer | **Used as it is** |
| Cannot be written (an exe under Program Files) | The embedded copy is used in memory — never an error |

- "Valid" is checked by **parsing**: the parsed entries are kept and handed to the catalog, the files are read once.
- Versions compare as `System.Version` (`major.minor.patch`).
- Each file is written through `<name>.tmp` then a replace, like `usage.json`; `version.txt` last.
- `EmojiCatalog` keeps the parsing and the categories; it reads from a `Stream`, the folder's file or the embedded
  resource alike.

### What a new list changes elsewhere

- **Pre-render cache**: its key already holds the hash of the emoji list (`EmojiBitmapCache.BuildKey`) — a new list is
  rendered again, no `FormatVersion` bump. The names and keywords are not in the key: they do not change the rendering.
- **Counters, custom groups, tray emoji**: kept by the emoji's text. An emoji the new list no longer has stays in the
  files and is not shown — already the rule.
- An emoji newer than the system font shows as a box — already the rule.

---

## Update Check

- **Settings menu ⚙ → `Check for emoji updates…`**, the last item, after a separator. The only network access of the
  app, and only on that click: no check at launch, no background check, no setting.
- While it runs, the item is greyed; the UI stays responsive (`async`, one `HttpClient`, 15 s timeout, a `User-Agent`
  naming the app).

### The flow

1. **Latest version**: `GET https://registry.npmjs.org/emojibase-data/latest` → its `version`. The `latest` tag never
   points at a pre-release.
2. **Compared** with the version in use (the folder's, or the embedded one when the folder could not be written):

   | Result | Message (owned by `MainForm`) |
   |---|---|
   | Not newer | `Emoji data is up to date (Emojibase 17.0.0).` — OK |
   | Network, HTTP or parse failure | `Could not check for emoji updates: <reason>` — OK, warning icon |
   | Newer | `Emojibase 18.0.0 is available (in use: 17.0.0). Update the emoji list, names and keywords?` — Yes / No, *Yes* the default |

3. **Yes → download** from jsDelivr: `https://cdn.jsdelivr.net/npm/emojibase-data@<version>/en/compact.json`,
   `…/fr/compact.json`, `…/LICENSE`.
4. **Validation**, before anything is written: both JSON files parse into non-empty entry lists, and the English one has
   emojis in the seven tabs' groups. Any newer major version is accepted when it passes: Emojibase's majors follow
   Unicode's emoji versions, they are the real updates.
5. **Written** to `emoji-data\` as a set (see *At launch*), `version.txt` holding the new version. A failure — download,
   validation, a folder that cannot be written — shows `Could not update the emoji data: <reason>` and leaves the
   folder as it was (the `.tmp` files deleted).
6. **Restart offered**: `Emoji data updated to Emojibase 18.0.0. Restart now to use it?` — Yes / No, *Yes* the default.
   - **Yes** → `Application.Restart()`: the app ends (not a `UserClosing`, so it is not hidden to the tray) and starts
     again with the same arguments, `--title` included.
   - **No** → used at the next launch.

---

## Documentation

| File | Change |
|---|---|
| `RULES.md` | New section *Emoji Data*: the folder, the launch table, the update flow; the *Window and Tray Icon* table gets the menu item; *Categories and Insertion* points to the folder instead of "embedded in the exe" |
| `README.md` / `README.fr.md` | The data folder next to the exe, `Check for emoji updates…`, the network access on demand |
| `SECURITY.md` | No longer "never connects to a network": only on `Check for emoji updates…`, to npm's registry and jsDelivr, nothing sent but the requests |
| `CONTRIBUTING.md` § Emoji data | The embedded copy recreates `emoji-data\`; an update of the embedded copy also replaces `version.txt` |
| `GLOSSARY.md` / `GLOSSARY.fr.md` | New term **Emoji data** (*données des émojis*): the Emojibase files the list, names and keywords come from, in `emoji-data\` next to the exe |

---

## Test Impact

The app has no test project (`CONTRIBUTING.md`: "check a change by hand in the running app"): no unit test is created
or updated. The checks are manual, in the running app:

| Behaviour to check | How | Create / Update |
|---|---|---|
| Folder missing → recreated, the app works | Delete `emoji-data\`, launch | — (manual) |
| Broken file → set rewritten | Truncate `compact.fr.json`, launch | — (manual) |
| Older version → replaced | Write `16.0.0` in `version.txt`, launch | — (manual) |
| Up to date | `Check for emoji updates…` with 17.0.0 | — (manual) |
| Newer version found | Write `16.0.0` in `version.txt` after launch, then check → offered, downloaded, restart | — (manual) |

---

## Open Questions

- [x] ~~How is the update check triggered?~~ → Manual only: a settings-menu item, no check without a click.
- [x] ~~When does an accepted update apply?~~ → A restart is offered; declined → at the next launch.
- [x] ~~What happens to a folder that exists but is broken or older than the exe's data?~~ → Rewritten from the
  embedded copy in both cases.

---

## Design Iterations

### Iteration 1 — 2026-10-08

- The user's direction: the Emojibase data as files next to the exe, used at launch; missing folder or files recreated
  from the embedded version; an option in the settings to check for updates and, when one is found, to offer to update.
- Scoping batch: manual check only, restart offered, broken or older folder rewritten, a straightforward subject.
- Proposed: the four-file set in `emoji-data\` (`version.txt` and `LICENSE` added), validation by parsing, the npm
  registry for the version and jsDelivr for the files, Yes / No dialogs owned by `MainForm`, `Application.Restart()`.
- Exploration: the pre-render cache key already follows the emoji list; `SECURITY.md` states the app never connects to
  a network — to be rewritten; no test project.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project — manual checks only |
| README | | | |
| RULES / SECURITY / CONTRIBUTING / GLOSSARY | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Can the Emojibase data be a file next to the exe, used at launch, to update it, even automatically later? | Step 1 yes, missing folder or files recreated from the embedded version; step 2 an option in the settings to check for updates and offer to update | 2026-10-08 |
| 2 | How is the update check triggered? | Manual only | 2026-10-08 |
| 3 | When does an accepted update apply? | Restart offered | 2026-10-08 |
| 4 | A folder that exists but is broken or older than the exe's data? | Repaired, and replaced when older | 2026-10-08 |
| 5 | Straightforward or tricky subject? | Straightforward | 2026-10-08 |

---

*Last updated: 2026-10-08*
