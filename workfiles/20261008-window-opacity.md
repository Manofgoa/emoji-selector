# Window Opacity

> Working document — a setting choosing the window's opacity among 100 %, 90 %, 80 % and 70 %.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The settings menu gets a **`Window opacity ▸`** submenu offering four values — `100%`, `90%`, `80%`,
`70%` — the one in use checked. The chosen opacity applies to the whole window, **always**: in
front or not. It is saved in `settings.json`, 100 % by default.

Components: `UI/MainForm.cs` (the menu, the opacity applied), `Data/SettingsFile.cs` (the key),
the reset's confirmation text, `RULES.md`, `README.md` / `README.fr.md`.

---

## Menu Item

- **`Window opacity ▸`**, in the settings menu's **window** section, after `Reset window size`
  (`MainForm.CreateSettingsMenu`) — the section of its feature, per RULES.md § Frame; the label
  says its subject on its own.
- Its submenu: `100%`, `90%`, `80%`, `70%`, top to bottom, the value in use **checked**
  (set at each opening of the menu, like the other check marks).
- A click on a value applies it **at once** and saves it; the window stays, the menu closes like
  any other item. A click on the checked value does nothing.
- No confirmation: nothing is lost.

---

## Applying the Opacity

- **`Form.Opacity`** (0.7 – 1.0): WinForms turns the window into a layered window
  (`WS_EX_LAYERED` + `SetLayeredWindowAttributes` / `LWA_ALPHA`) below 1.0. The whole window
  fades — search bar, tabs, grid, details panel — not only its background.
- **Always**: in front or not, hidden then shown again, placed by Win+; or the tray icon alike.
- Set in the constructor from `settings.json`, before the handle exists: a `--background` launch
  shows it already at its opacity on the first show.
- The **menus and dialogs** (settings menu, an emoji's right-click menu, the group name dialog,
  the colour dialog, the message boxes) are windows of their own: they stay **opaque**.
- **To check during the run** (risk, not a design question): the frame stays Windows' own under
  the layered style — the shadow, the rounded corners, the borders of `WM_NCCALCSIZE` /
  `WM_NCHITTEST` (RULES.md § Frame) — and the colour emojis of `EmojiRenderer` keep drawing.

---

## Settings File

- A new key **`opacity`** in `settings.json`: the **percentage**, an integer (`100`, `90`, `80`,
  `70`) — `SettingsFile.ReadOpacity` / `WriteOpacity`, like `showFrench` / `highlightColor`.
- **Missing key, missing file, unreadable value** (not an integer) → 100 %.
- **An integer that is not one of the four** (written by hand) → the **nearest** of the four; a
  tie goes to the more opaque one: `85` → 90 %, `75` → 80 %, `50` → 70 %, `120` → 100 %. The key
  is left as it is until the next choice; the menu checks the value applied.
- `100` is written like the others when chosen (the key is not removed): the file says what the
  user picked.
- **Reset all settings** deletes `settings.json` already: back to 100 % after the restart. Its
  confirmation names it among what is lost: *"the window size and opacity, the tray emoji, the
  details panel's settings"* (`MainForm`'s reset confirmation text).
- **Reset window size** does not touch the opacity.

---

## Documentation

- `RULES.md` § Window and Tray Icon: a row for `Window opacity ▸`; § Frame: the *window* section lists
  it; § Size: the `opacity` key in the `settings.json` description; § Reset All Settings: the
  confirmation's list names the opacity.
- `README.md` / `README.fr.md`: the item in the settings menu's description, same commit.
- `GLOSSARY.md` / `GLOSSARY.fr.md`: no new term.

---

## Test Impact

There is **no test project** in this repository (CONTRIBUTING.md § Build: *check a change by hand
in the running app*): nothing is created nor updated. Checked by hand, and by script on the running
check instance where it can be:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| No `opacity` key → the window opaque, `100%` checked | — (by hand) | — |
| A value clicked → the window at that opacity at once, `opacity` written, the other keys kept | — (by hand; the file read back) | — |
| A relaunch → the saved opacity applied from the first show, `--background` launch included | — (by hand; `GetLayeredWindowAttributes` from a script) | — |
| An `opacity` value written by hand: not an integer → 100 %; `85` → 90 %, `75` → 80 %, `50` → 70 %, `120` → 100 % | — (by hand) | — |
| The frame keeps its shadow, rounded corners, resize borders and drag area below 100 % | — (by hand) | — |
| The menus and dialogs stay opaque | — (by hand) | — |
| `Window opacity ▸` sits in the *window* section, after `Reset window size` | — (by hand) | — |
| Reset all settings' confirmation names the opacity | — (by hand) | — |

---

## Open Questions

- [x] ~~**1. An `opacity` value that is not one of the four** (e.g. `85` or `50`, written by
  hand)?~~ → The nearest of the four, a tie to the more opaque one
- [x] ~~**2. Label of the submenu?**~~ → `Window opacity ▸`
- [x] ~~**3. Reset all settings' confirmation**: add the opacity to what is lost?~~ → Yes, *"the
  window size and opacity, the tray emoji, the details panel's settings"*

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-08

Initial design from the request (*"Settings pour gérer l'opacité : 100% 90% 80% 70%"*) and the
scoping answers: an `Opacity ▸` submenu in the settings menu's window section, the four values,
the one in use checked; the opacity applied to the whole window always, through `Form.Opacity`;
saved as `opacity` (a percentage) in `settings.json`, 100 % by default. No test project: checked by
hand. Three open questions: an invalid saved value, the label, the reset's confirmation text. No
row of `TODO-FEATURES.md` matches the request.

### Iteration 2 — 2026-10-08

The three open questions answered: an `opacity` value that is not one of the four maps to the
**nearest** one, a tie to the more opaque (`85` → 90 %); the submenu is labelled **`Window opacity
▸`**, not `Opacity ▸`; the reset's confirmation names the opacity. No question left.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project — checked by hand |
| RULES.md | | | |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Where does the opacity choice go in the settings menu? (an `Opacity ▸` submenu in the *window* section / four flat items) | `Opacity ▸` submenu | 2026-10-08 |
| 2 | When does the chosen opacity apply? (always / only while the window is inactive) | Always | 2026-10-08 |
| 3 | Is the opacity kept between launches? (yes, in `settings.json`, 100 % by default / no) | Yes, `settings.json` | 2026-10-08 |
| 4 | Is the subject straightforward or tricky / long to explore? | Straightforward | 2026-10-08 |
| 5 | An `opacity` value that is not one of the four: 100 %, or the nearest value? | The nearest value | 2026-10-08 |
| 6 | Label of the submenu: `Opacity ▸` or `Window opacity ▸`? | `Window opacity ▸` | 2026-10-08 |
| 7 | Reset all settings' confirmation: add the opacity to what is lost? | Yes | 2026-10-08 |

---

*Last updated: 2026-10-08*
