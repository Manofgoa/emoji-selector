# Window Opacity

> Working document — a setting choosing the window's opacity among 100 %, 90 %, 80 % and 70 %.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The settings menu gets an **`Opacity ▸`** submenu offering four values — `100%`, `90%`, `80%`,
`70%` — the one in use checked. The chosen opacity applies to the whole window, **always**: in
front or not. It is saved in `settings.json`, 100 % by default.

Components: `UI/MainForm.cs` (the menu, the opacity applied), `Data/SettingsFile.cs` (the key),
the reset's confirmation text, `RULES.md`, `README.md` / `README.fr.md`.

---

## Menu Item

- **`Opacity ▸`**, in the settings menu's **window** section, after `Reset window size`
  (`MainForm.CreateSettingsMenu`) — the section of its feature, per RULES.md § Frame.
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
- **Missing key, missing file, unreadable value** → 100 %. *(An integer that is not one of the
  four values: see Open Question 1.)*
- `100` is written like the others when chosen (the key is not removed): the file says what the
  user picked.
- **Reset all settings** deletes `settings.json` already: back to 100 % after the restart. Its
  confirmation lists what is lost — *see Open Question 3*.
- **Reset window size** does not touch the opacity.

---

## Documentation

- `RULES.md` § Window and Tray Icon: a row for `Opacity ▸`; § Frame: the *window* section lists
  it; § Size: the `opacity` key in the `settings.json` description; § Reset All Settings: the
  confirmation's list, if it changes (Open Question 3).
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
| An invalid `opacity` value written by hand → 100 % (or per Open Question 1) | — (by hand) | — |
| The frame keeps its shadow, rounded corners, resize borders and drag area below 100 % | — (by hand) | — |
| The menus and dialogs stay opaque | — (by hand) | — |
| `Opacity ▸` sits in the *window* section, after `Reset window size` | — (by hand) | — |

---

## Open Questions

- [ ] **1. An `opacity` value that is not one of the four** (e.g. `85` or `50`, written by hand)?
  Proposal: **100 %**, the key left as it is until the next choice — the menu then checks
  `100%`. Alternative: the nearest of the four values.
- [ ] **2. Label of the submenu?** Proposal: **`Opacity ▸`** — the *window* section gives it its
  subject. Alternative: `Window opacity ▸`.
- [ ] **3. Reset all settings' confirmation**: it lists *"the window size, the tray emoji, the
  details panel's settings"* — add the opacity? Proposal: **yes**, *"the window size and opacity,
  the tray emoji, the details panel's settings"*.

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
| 5 | An `opacity` value that is not one of the four: 100 %, or the nearest value? | | |
| 6 | Label of the submenu: `Opacity ▸` or `Window opacity ▸`? | | |
| 7 | Reset all settings' confirmation: add the opacity to what is lost? | | |

---

*Last updated: 2026-10-08*
