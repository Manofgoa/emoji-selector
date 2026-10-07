# Borderless Window

> Working document — the Windows title bar removed, a close cross and a settings button drawn at
> the right of the category tabs.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The window loses its Windows **title bar** (caption, icon, minimize / maximize / close buttons), like
the Win+; panel it replaces. What the title bar gave is redistributed:

| Title bar gave | Becomes |
|---|---|
| Close button ✕ | A **close cross** drawn at the right end of the tab strip — hides to the tray, like ✕ today |
| Moving the window (drag the caption) | Dragging the **empty part of the tab strip**, between the last tab and the cross |
| Resizing (borders) | Unchanged: the borders stay resizable |
| Minimize _ | **Removed** |
| Maximize ☐, double-click on the caption | **Removed** — the window can no longer be maximized |
| The second title, shown in the caption | No longer shown **in** the window; still in the taskbar, Alt+Tab and the tray icon's tooltip (`Form.Text` unchanged) |

New, next to the cross: a **settings button** (gear) opening a menu, holding for now a single item
that opens the exe's folder in the File Explorer.

Components: `UI/MainForm.cs` (frame, hit-testing, the minimize / maximize code going away),
`UI/CategoryTabStrip.cs` (the cross, the settings button, the drag area), `RULES.md`, `README.md` / `README.fr.md`.
`UI/WindowPlacement.cs` and `Input/` are not touched.

---

## Window Frame

- **The caption is removed, the frame is kept**: `MainForm` handles `WM_NCCALCSIZE` so the client
  area covers the caption — the default computation is run, then its top is put back to the window's
  top. The left, right and bottom resize borders stay Windows' own, and so does the DWM frame:
  the **shadow**, and the **rounded corners** on Windows 11.
  - Not `FormBorderStyle.None`: it loses the shadow, the rounded corners and the resize borders, all
    to be redrawn by hand.
- **Top edge**: the caption took the top resize border with it — `WM_NCHITTEST` answers `HTTOP`
  (`HTTOPLEFT` / `HTTOPRIGHT` at the corners) on the top few pixels of the window, the same
  thickness as the side borders (`SM_CXSIZEFRAME` + `SM_CXPADDEDBORDER`, DPI-scaled). The messages,
  codes and that thickness live in `UI/WindowFrame.cs`.
- Windows asks the **child control under the mouse** first: the search bar on top (`MainForm.SearchBar`)
  and the tab strip answer `HTTRANSPARENT` over the top band, so the hit test reaches the window.
- **No minimize, no maximize**: `MinimizeBox = false`, `MaximizeBox = false`. Windows then refuses
  Win+Up, Win+Down to minimize, the drag-to-top snap and the double-click on the drag area.
- **Size**: `ClientSize` is set to 440 × 450 — wide enough for the minimum width below. WinForms
  computes the window from it with the caption, which then becomes client area: the window keeps
  that outer size, the grid gaining the caption's height. `MinimumSize` is the strip's
  `LogicalMinimumWidth` plus two side borders of 8 logical pixels, 240 high as before (see
  *Tab Strip Layout*).
- **Alt+F4** still closes (`CloseReason.UserClosing` → hidden to the tray), unchanged.

### Code Going Away

The window can be neither minimized nor maximized any more:

- `restoreState` and the `OnResize` override (minimized → hidden) are removed.
- `OnTrayIconClicked` loses its "restore if minimized" step.
- `OnShortcutPressed` loses its maximized branch and its minimized handling: the window is always
  placed, shown, placed again.

### Placement

Unchanged. `PlaceAt` reads the visible frame with `DWMWA_EXTENDED_FRAME_BOUNDS` and keeps the
invisible borders out of the placement: with no caption, the frame simply starts higher. `IsCovered`
reads the same bounds.

---

## Tab Strip Layout

From left to right:

| Zone | Width (logical px) | Does |
|---|---|---|
| Left padding | 4 | Nothing (as today) |
| Tabs | 44 each, 7 | Click → scrolls the grid to the category (as today) |
| **Drag area** | The rest, at least 24 | Moves the window |
| **Settings button** | 46, the full height of the strip | Opens the settings menu |
| **Close cross** | 46, the full height of the strip | Hides the window to the tray |

- **Minimum width**: 4 + 7 × 44 + 24 + 46 + 46 = **428** logical pixels of client area —
  `MinimumSize` follows (the borders added), DPI-scaled as today.
- The separator line under the strip runs under the buttons and the drag area too.

### Settings Button

- Glyph **Settings** (`U+E713`, a gear) of the tab icon font, 16 logical pixels like the tab glyphs,
  grey (`SystemColors.GrayText`).
- **Hover**: the tabs' grey background (`SystemColors.ControlLight`) — only the cross turns red.
- **Tooltip** `Settings`.
- **Click** (left button): the strip raises a new `SettingsClicked` event with the button's bounds;
  `MainForm` shows the **settings menu** right under the button, its right edge aligned with the
  button's — a `ContextMenuStrip`, like the tray icon's menu. The button stays drawn as hovered while
  the menu is open (`SettingsMenuOpen`).
- A press on the button **closing** its open menu does not open it again: a press within 250 ms of
  the menu closing under a left press over the button is ignored.
- The settings button and the cross are **never greyed** by a search: only the tabs are.
- Not selectable, no keyboard focus, like the tabs and the cross.

### Settings Menu

Owned by `MainForm` (`CreateSettingsMenu`, `OpenAppFolder`): a few lines, no file of its own. For
now, one item:

| Item | Does |
|---|---|
| `Open app folder` | Opens the folder holding the exe in the File Explorer, **the exe selected** in it |

- The File Explorer is started with `Process.Start("explorer.exe", ...)` and the argument
  `/select,"<exe path>"` (`Environment.ProcessPath`), never through a shell command line.
- **The window stays** as it is once the item is clicked: the File Explorer simply comes in front
  of it.

### Close Cross

- Glyph **ChromeClose** (`U+E8BB`) of the tab icon font (Segoe Fluent Icons, Segoe MDL2 Assets on
  Windows 10), 10 logical pixels, grey (`SystemColors.GrayText`) like the inactive tabs.
- **Hover**: Windows' own close button — red background `#C42B1C`, white glyph; pressed, the
  same red a little lighter (`#C7493C`). The tabs keep their grey hover.
- **Tooltip** `Close`, like the tabs' category names.
- **Click** (left button, released over the cross): the strip raises a new `CloseClicked` event;
  `MainForm` answers with `this.Close()` — `CloseReason.UserClosing`, so the existing
  `OnFormClosing` hides it to the tray. One path for the cross, Alt+F4 and a scripted `SC_CLOSE`.
- Not selectable, no keyboard focus, like the tabs.

### Drag Area

- The strip answers `WM_NCHITTEST` with `HTTRANSPARENT` over the drag area; `MainForm` answers
  `HTCAPTION` for those points. Windows then moves the window itself: a native drag, Aero snap to the
  sides of the screen (left / right halves — the top snap needs maximize, refused).
- **Right click** on the drag area: Windows' **system menu**, native to `HTCAPTION` — Move, Size,
  Close; minimize and maximize greyed.
- No visual mark: an empty band, like the caption it replaces.

### With the Search Box

The search box (`20261007-search-box.md`) docks **above** the tab strip — it was merged into `main`
before this run started. The drag area, the settings button and the cross stay **on the tab row**:
the window's top row is the search box, the second the tabs. The docking order is unchanged; the
search bar became a `TableLayoutPanel` subclass letting the top resize band through.

---

## Documentation

| File | Change |
|---|---|
| `RULES.md` § Command-Line Arguments | The second title is shown in the taskbar, Alt+Tab and the tooltip — no longer "in the window title bar" |
| `RULES.md` § Window and Tray Icon | Table: *Close cross* (and Alt+F4) hide to the tray; the *Minimize* row removed; a *drag area* row; a *settings button* row and its menu item. The note on scripted checks: `SC_CLOSE` still works. A paragraph on the frame (no caption, resizable, never minimized nor maximized) |
| `RULES.md` § Shortcut | "A window last maximized comes back maximized" removed |
| `README.md` + `README.fr.md` | Second title: no longer "in its title bar". Tray icon: the window's **close cross** (and Alt+F4) hide it; the minimize button gone. A line: no title bar, moved by dragging the empty part of the tab strip. A line: the settings button and its menu item |
| `GLOSSARY.md` + `GLOSSARY.fr.md` | Nothing: *drag area* and *close cross* are UI parts, not domain terms |

---

## Test Impact

**No unit test** — there is no test project (CONTRIBUTING § Build), and the earlier workfiles check
by hand (global hotkey, Q&A 8); the user kept that choice here (Q&A 7).

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none (checked by hand) | — | — |

Checked by hand in the launched app: no caption; the four borders and corners resize; dragging the
empty band moves the window, snaps left / right; the cross hides to the tray, its tooltip; Alt+F4
hides; the gear opens its menu under itself, the item opens the exe's folder; Win+Up / Win+Down do nothing; Win+; still places the window under the text cursor; the
taskbar and the tray tooltip still show the second title; Windows 11 rounded corners and shadow.

---

## Open Questions

- [x] ~~1. The search box goes above the tab strip: keep the drag area and the cross on the tab
  row, or move them to a top row shared with the box?~~ → On the tab row
- [x] ~~2. The cross's hover look: Windows' own red, or the tabs' grey?~~ → Windows' own red
- [x] ~~3. Unit tests: none and checked by hand, or create a test project?~~ → None, checked by hand
- [x] ~~4. Right click on the drag area: Windows' system menu, or nothing?~~ → The system menu
- [x] ~~5. The menu item's label?~~ → `Open app folder`
- [x] ~~6. Open the folder plainly, or with the exe selected?~~ → The exe selected (`explorer /select`)
- [x] ~~7. Once the item is clicked: the window stays, or hides to the tray?~~ → It stays

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-08

Initial design, from the user's request and the scoping answers (Q&A 1–4): the cross hides to the
tray like ✕; the borders stay resizable and the empty part of the tab strip moves the window;
minimize and maximize removed, the second title kept out of the window (taskbar, Alt+Tab, tooltip).
The user rated the subject straightforward: a single scout pass, done directly (the questions
chained: frame → tab strip → placement).

Proposed: caption removed through `WM_NCCALCSIZE` (shadow, rounded corners and resize borders kept),
top border re-created by `WM_NCHITTEST`; the drag area answered `HTCAPTION`; the cross a new zone of
`CategoryTabStrip` raising `CloseClicked` → `Close()`; the minimize / maximize code removed;
minimum width 382 logical pixels. No backlog row matches the request.

### Iteration 2 — 2026-10-08

Open Questions 1–4 answered (Q&A 5–8): the cross and the drag area stay on the tab row once the
search box is merged above it; the cross turns Windows red on hover; no test project, checked by
hand; a right click on the drag area opens Windows' system menu. No open question remains.

### Iteration 3 — 2026-10-08

User request: a **settings button** (gear icon) next to the close cross, opening a menu that holds,
to begin with, a single item opening the exe's folder in the File Explorer.

Proposed: the gear left of the cross, same width, grey hover like the tabs; a `ContextMenuStrip`
shown under it, like the tray icon's menu; the folder opened with `Process.Start`. The minimum width
grows to 428 logical pixels. Three points left open (Open Questions 5–7): the item's label, opening
the folder plainly or with the exe selected, and whether the window hides once the item is clicked.

### Iteration 4 — 2026-10-08

Open Questions 5–7 answered (Q&A 9–11): the item reads `Open app folder`, opens the folder with the
exe selected, and the window stays once it is clicked. No open question remains.

### Iteration 5 — 2026-10-08 — ✅ Implemented

Go given: code, unit tests and documentation (no unit test, as designed), in a worktree —
`.claude/worktrees/borderless-window`, branch `feature/borderless-window`, created from `db6c663`.

### Iteration 6 — 2026-10-08 — 🧭 Implementation choices

No project rule broken. Choices the frozen design did not state:

- **The search box was already in `main`** when the run started (merged while this workfile was
  designed): the top resize band falls over the search bar, not the tab strip. The bar became
  `MainForm.SearchBar`, a `TableLayoutPanel` answering `HTTRANSPARENT` there; the tab strip does the
  same over the band, should it ever be on top.
- **Window size**: `ClientSize` 440 × 450 instead of 400 × 450 — the design's minimum width (428)
  exceeded 400. The window keeps the outer size WinForms computes with the caption; the grid gains
  the caption's height rather than the window shrinking.
- **`MinimumSize`** = the strip's `LogicalMinimumWidth` + 2 × 8 logical pixels of side borders
  (a constant: the metric at 96 DPI), height 240 kept.
- **`UI/WindowFrame.cs`**, a new static class: the `WM_NCCALCSIZE` / `WM_NCHITTEST` messages, the
  hit-test codes, the border thickness (`GetSystemMetricsForDpi`), shared by the form and the strip.
- `SWP_FRAMECHANGED` sent once the handle exists, so the frame is computed again with the caption
  removed.
- **Buttons never greyed** by a search; a press closing the settings menu does not reopen it
  (250 ms window).
- The settings menu stays in `MainForm` — no `UI/SettingsMenu.cs`.
- **Checks**: the frame was checked in the launched app (no caption, rounded corners and shadow,
  cross right of the tabs, gear left of it). The settings menu could **not** be checked from a
  script: a click posted to the strip showed no menu — most likely because the app was not in the
  foreground, so the result proves nothing. The user was moving the mouse over the window at the
  same time, so the run stopped simulating input. The menu is left to the hand test. One scripted
  click went astray before the window was up, at the screen's left edge (0, 84).

### Iteration 7 — 2026-10-08 — ⚙️ Post-implementation — Settings menu never opening

Reported by the user after the merge into `main`: the gear's menu never shows. Cause: the time the
menu last closed under a press on the gear started at `long.MinValue`, and
`Environment.TickCount64 - long.MinValue` overflows to a negative number — always below 250 ms, so
every press on the gear was taken for the press closing the menu, and ignored. Fix: the field becomes
a `long?`, null until such a closing; a null time never matches. The design is unchanged. Fixed
directly on `main` (user's choice).

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 5, 6, 7 | 2026-10-08 | Frame (`WindowFrame`, `MainForm`), close cross and drag area, settings button and menu — three commits; the menu-never-opening fix on `main` (iteration 7) |
| Unit tests | 5 | 2026-10-08 | None, as designed (Q&A 7): no test project |
| README | 5 | 2026-10-08 | `README.md` + `README.fr.md`: second title, tray icon, a *Window* feature line |
| RULES | 5 | 2026-10-08 | § Command-Line Arguments, § Window and Tray Icon (table + new § Frame), § Shortcut |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What does the cross at the right of the tabs do? | Hides the window to the tray, like ✕ today | 2026-10-08 |
| 2 | Without a title bar, how is the window moved and resized? | Resizable borders + dragging the empty part of the tab strip | 2026-10-08 |
| 3 | What becomes of minimize, maximize and the second title? | Minimize and maximize removed; the second title only in the taskbar, Alt+Tab and the tooltip | 2026-10-08 |
| 4 | Exploration depth? | Straightforward | 2026-10-08 |
| 5 | OQ1 — With the search box above the tabs, where do the cross and the drag area go? | On the tab row | 2026-10-08 |
| 6 | OQ2 — The cross's hover look? | Windows' own red | 2026-10-08 |
| 7 | OQ3 — Unit tests? | None, checked by hand | 2026-10-08 |
| 8 | OQ4 — Right click on the drag area? | Windows' system menu | 2026-10-08 |
| 9 | OQ5 — The menu item's label? | `Open app folder` | 2026-10-08 |
| 10 | OQ6 — Folder opened plainly, or with the exe selected? | The exe selected | 2026-10-08 |
| 11 | OQ7 — Once the item is clicked, does the window stay or hide? | It stays | 2026-10-08 |

---

*Last updated: 2026-10-08*
