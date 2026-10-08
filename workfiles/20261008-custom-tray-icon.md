# Custom Tray Icon

> Working document — the tray icon shows an emoji the user chose (right click → `Use as tray icon`),
> remembered in `settings.json`, 🙂 by default; it no longer follows the last emoji used.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the **tray icon** shows the last emoji used, starting on 😊 at every launch, never persisted
(`UI/TrayIcon.cs`, `MainForm.OnEmojiUsed`). It becomes a setting:

- **Default**: 🙂 (*slightly smiling face*, U+1F642) — replacing 😊.
- **Using an emoji no longer changes the icon.** `OnEmojiUsed` keeps counting the use; it stops
  calling `TrayIcon.ShowEmoji`.
- **Chosen by right click** on an emoji of the grid: a new `Use as tray icon` item.
- **Remembered** in `settings.json`, the app's shared settings file, and shown again at the next launch.
- **No dedicated reset**: going back to 🙂 is choosing 🙂 with the same right click.

Components touched: `UI/TrayIcon.cs`, `Data/SettingsFile.cs`, `UI/MainForm.cs`; docs `RULES.md`,
`README.md` / `README.fr.md`, `GLOSSARY.md` / `GLOSSARY.fr.md`.

---

## Right-Click Menu

The menu of a right-clicked emoji (`MainForm.ShowEmojiMenu`), in every section — the catalog's, the
frequent one, the custom groups, the search results — like `Add to ▸`.

| Item | Position | State | Click |
|---|---|---|---|
| `Use as tray icon` (`MainForm.UseAsTrayIconText`) | **First**, above `Add to ▸`, a separator after it | **Checked** when that emoji is the current tray icon | The tray icon shows the emoji at once, and it is saved |
| `Add to ▸`, `Remove` | Unchanged, after the separator | Unchanged | Unchanged |

- Clicking it while it is checked changes nothing (the emoji already is the icon).
- The window's state after the click: see *Open Questions*.
- The emoji compared and saved is the **catalog's text** (`FE0F` included), as the custom groups do:
  the menu is only ever built on a grid emoji, so it always is one.

---

## Tray Icon

| Point | Behaviour |
|---|---|
| Default | `TrayIcon.DefaultEmoji` becomes 🙂 `"\U0001F642"` |
| At launch | `MainForm` reads the saved emoji and gives it to `TrayIcon`'s constructor; none, or one it cannot show (see *Settings File*) → the default |
| Current emoji | `TrayIcon` exposes the emoji it shows (`Emoji`), read by the menu to check its item |
| On use | `OnEmojiUsed` no longer calls `ShowEmoji`: the icon only changes through the menu |
| Tooltip, clicks, `Exit` | Unchanged |

---

## Settings File

`settings.json` (`Data/SettingsFile.cs`) gets a third key next to `windowWidth` / `windowHeight`:

```json
{ "windowWidth": 640, "windowHeight": 480, "trayEmoji": "🙂" }
```

| Point | Behaviour |
|---|---|
| Key | `trayEmoji`: the emoji's text |
| Read | `SettingsFile.ReadTrayEmoji()`: a non-blank string, else null (missing key, wrong type, unreadable file) |
| Write | `SettingsFile.WriteTrayEmoji(string)`: sets the key, keeps the other keys, through `settings.json.new` then a replace — like the size |
| Readable | Written as the emoji itself, not `\uXXXX` escapes: the JSON goes through `EmojiUsage.ReadableEmojis`, as `usage.json` and `custom-groups.json` do |
| Unknown emoji | An emoji the catalog does not have (a typo, an older catalog) → the default shown; what happens to the key: see *Open Questions* |
| Failure | A folder that cannot be written → the choice lasts until the app ends, never an error |

---

## Documentation

| File | Change |
|---|---|
| `RULES.md` | § Window and Tray Icon: the icon shows the chosen emoji, 🙂 by default, saved in `settings.json`; the right-click row gets `Use as tray icon`; the *last emoji used* bullet goes; `settings.json`'s keys (§ Size) gain `trayEmoji`; § Custom Tab's right-click bullet mentions the new first item |
| `README.md` + `README.fr.md` | *Tray icon*: the smiley 🙂, changed by right click; *Insertion*: the icon no longer shows the inserted emoji |
| `GLOSSARY.md` + `GLOSSARY.fr.md` | *Tray icon*: "it shows the emoji the user chose, 🙂 by default" in place of "the last emoji used" |

---

## Test Impact

No unit test: the app has no test project (`CONTRIBUTING.md`: "There is no test project: check a
change by hand in the running app"). Checked by hand in the launched app:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| First launch without `trayEmoji` → 🙂 in the tray | — (manual) | — |
| Inserting an emoji leaves the tray icon unchanged | — (manual) | — |
| Right click → `Use as tray icon` is first, checked on the current icon's emoji, changes the icon | — (manual) | — |
| The choice is in `settings.json`, readable, the window size kept; shown again after a relaunch | — (manual) | — |

---

## Open Questions

- [ ] After a click on `Use as tray icon`, does the window stay open (like the settings menu's
  items) or hide to the tray (like an insertion)? Proposed: stays open.
- [ ] Choosing 🙂, the default: saved like any other emoji, or the `trayEmoji` key removed (a later
  change of the default would then apply)? Proposed: the key removed.
- [ ] A saved `trayEmoji` the catalog does not have: the default shown and the key left as it is
  until the next choice, or removed at launch? Proposed: left as it is.

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-08

Initial design, from the request and the scoping batch (Q&A 1–3): default 🙂; the tray icon no
longer follows the last emoji used; `Use as tray icon` first in the emoji's right-click menu,
checked on the current one; no dedicated reset, the right click choosing 🙂 again is enough;
`trayEmoji` in `settings.json`. No matching row in `TODO-FEATURES.md`. Single scout pass (the
subject was rated straightforward): `TrayIcon.ShowEmoji` has one caller, `OnEmojiUsed`;
`SettingsFile` holds the window size only; no test project.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable — no test project, checked by hand |
| README | | | |
| RULES.md | | | |
| GLOSSARY | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Default tray emoji (today 😊)? | 🙂 simple smile | 2026-10-08 |
| 2 | How does the right-click item look? | `Use as tray icon`, at the top, checked when it is already the current icon | 2026-10-08 |
| 3 | How to go back to the default icon? | Right click only — choosing the default emoji, no dedicated item | 2026-10-08 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward | 2026-10-08 |
| 5 | After `Use as tray icon`, does the window stay open or hide? | | |
| 6 | Choosing 🙂: saved, or the key removed? | | |
| 7 | A saved emoji the catalog does not have: key left or removed? | | |

---

*Last updated: 2026-10-08*
