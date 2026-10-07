# Default Window Size

> Working document — a default window size computed from the grid (16 columns, a section header and
> 8 rows of emojis), and the size the user resizes to remembered between launches.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the window opens at a fixed `ClientSize` of **440 × 450** logical pixels (`UI/MainForm.cs`),
which holds no round number of columns. The user wants the default size to be **measured in emojis**:

- **Width**: exactly **16 columns** of emojis.
- **Height**: the window opened at the top of a category shows its **section header, then exactly 8
  full rows** of emojis.

Once the user resizes the window, that size is **remembered between launches**: 16 × 8 is only the
size of the first launch (or of a launch whose saved size cannot be read).

| In scope | Out of scope |
|---|---|
| The default size computed from the grid's metrics, at any DPI | The minimum size (`MinimumSize`, unchanged) |
| The size saved in `settings.json` when the user resizes, reloaded at the next launch | Where the window appears (Win+; under the text cursor, centred at launch — unchanged); the position is never saved |
| A saved size that does not fit the screen, or cannot be read → handled without error | The grid's metrics themselves (cell 40, header 32, padding 8) |
| A *Reset window size* item in the settings menu | |

---

## Current State (codebase)

| Fact | Where |
|---|---|
| `ClientSize = new Size(440, 450)`, `StartPosition = CenterScreen`, `AutoScaleMode.Dpi` from 96 DPI | `UI/MainForm.cs` constructor |
| `MinimumSize` = the tab strip's minimum width + side borders, 240 high | `UI/MainForm.cs` |
| Client area, top to bottom: search bar (auto-sized), tab strip, grid (fills the rest) | `UI/MainForm.cs` |
| Grid metrics, logical pixels: cell **40**, header **32**, side padding **8**; a vertical scroll bar docked right | `UI/EmojiGrid.cs` |
| Columns = `(grid width − scroll bar − 2 × padding) / cell`; the first header sits at y = 0 (no top padding) | `UI/EmojiGridLayout.cs` |
| Nothing is persisted about the window; files next to the exe: `cache\` (emoji atlases), `usage.json` (frequent tab, merged during the run) | `Drawing/EmojiBitmapCache.cs`, `workfiles/20261008-frequent-tab.md` |
| No test project: changes are checked by hand | `CONTRIBUTING.md` |

---

## Default Size

Computed, not hard-coded: the numbers follow the grid's metrics and the DPI.

- **Grid width** = `16 × cell + 2 × padding + scroll bar width` → at 96 DPI: 16 × 40 + 2 × 8 + the
  system scroll bar (~17) ≈ **673** logical pixels.
- **Grid height** = `header + 8 × cell` → at 96 DPI: 32 + 8 × 40 = **352**.
- **Client size** = grid width × (search bar height + tab strip height + grid height). The grid's
  part comes from `EmojiGrid.SizeFor(columns, rows)`; the search bar is measured by its **preferred
  height** — before the first show its AutoSize has not applied yet, and its `Height` still reads 100.
- The column and row counts live in **two named constants** (`MainForm.DefaultColumns = 16`,
  `DefaultRows = 8`), next to the computation (`MainForm.DefaultClientSize`).
- Computed in **`MainForm.OnLoad`**, before `base.OnLoad` centres the window: the handle exists, at
  the DPI of its monitor.
- At another DPI, every term is scaled the same way: 16 columns and header + 8 rows at any scale.
  Checked at 125 %: client 841 × 555, grid 841 × 440 — 16 columns, a header and 8 full rows.
- The rows measured are the **category rows** (cell 40). The frequent section, which the window
  opens on since the frequent tab was merged, has taller captioned cells (54): at the top of the grid
  the window shows that section, then the categories, not 8 rows of one section.
- **Sizing the window**: `MainForm.SetClientArea(clientSize)` sets the window's `Size` from the
  borders Windows draws (`GetWindowRect` − `GetClientRect`). The `ClientSize` setter is never used:
  it counts a caption, which is client area here (see `RULES.md` § *Frame*), and the window came out
  a caption too tall (+38 px at 125 %).
- Larger than the monitor's working area (small screen, high scale) → reduced so the whole window,
  its invisible resize borders included, fits it.

---

## Remembered Size

- **Saved**: the window's client size when the user **finishes a resize** (`OnResizeEnd`) — not at
  exit, since an exit by Windows shutting down or the Task Manager may never run the app's code.
  Compared with the size at `OnResizeBegin`, in logical pixels: a move, or a drag to a monitor of
  another scale, saves nothing.
- **Stored in logical pixels** (96 DPI): reloaded on a monitor of another scale, the window holds the
  same number of columns and rows.
- **Reloaded** at launch, in place of the default size.
- **Fallback** — no file, unreadable or invalid content, a folder that cannot be written → the
  default size, never an error (like `cache\` and `usage.json`).
- A saved size smaller than `MinimumSize` → `MinimumSize`; larger than the working area → reduced to
  fit it.
- **The size only**: the position is never saved — at launch the window stays centred, and Win+;
  places it under the text cursor anyway.
- **File**: `settings.json`, **next to the exe** (`AppContext.BaseDirectory`), like `usage.json` — a
  shared settings file, ready for later settings. Its content:

  ```json
  { "windowWidth": 673, "windowHeight": 520 }
  ```

  Both values in logical pixels. A write keeps the file's other keys, should later settings add some,
  and goes through `settings.json.new` then a replace. Read and written by `Data/SettingsFile.cs`.

---

## Reset Window Size

- A **`Reset window size`** item in the settings menu (⚙), after *Open app folder* and before
  *Clear frequently used* (merged from `main` during the run).
- It brings the window back to the **default size** right away — its top-left corner stays, the
  window reduced to fit the working area if needed — and **removes the saved size** from
  `settings.json`: the next launch opens at the default size too.
- The window stays shown, like after *Open app folder*.
- Always enabled, even with no saved size: it then only resizes the window.

---

## Documentation Impact

| File | Change |
|---|---|
| `RULES.md` / *Window and Tray Icon* | The default size (16 columns, header + 8 rows, computed), the remembered size (when saved, `settings.json`, the fallbacks); a row for *Reset window size* in the actions table |
| `README.md` / `README.fr.md` | *Window* bullet: opens 16 emojis wide and 8 rows high; remembers the size it is resized to; the gear's **Reset window size** |

---

## Test Impact

There is no test project (`CONTRIBUTING.md`): every behaviour is checked by hand in the running app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| First launch (no saved size): exactly 16 columns, header + 8 full rows at the top of a category | — (manual) | — |
| Same at 125 % / 150 % scale | — (manual) | — |
| Resized, then relaunched → the resized size comes back | — (manual) | — |
| Invalid or missing saved file, read-only folder → default size, no error | — (manual) | — |
| Saved size larger than the screen → fits the working area | — (manual) | — |
| A resize writes `settings.json` next to the exe, other keys kept | — (manual) | — |
| *Reset window size* → default size now, saved size removed, default at the next launch | — (manual) | — |

---

## Open Questions

- [x] ~~How are the 8 rows counted, given the section headers?~~ → The window opened at the top of a
  category shows its header, then 8 full rows
- [x] ~~What happens after the user resizes?~~ → The size is remembered between launches
- [x] ~~Is the window's **position** remembered too, or only its size?~~ → The size only
- [x] ~~Where is the size saved: its own `window.json` next to the exe (like `usage.json`), or a
  shared `settings.json` next to the exe, ready for later settings?~~ → `settings.json` next to the exe
- [x] ~~A way back to the default size — a *Reset window size* item in the settings menu (next to
  *Open app folder*) — or none?~~ → A *Reset window size* item in the settings menu

---

## Design Iterations

### Iteration 1 — 2026-10-08

Request: default size of 16 emoji columns, height of "6" rows — revised to **header + 8 rows** in
the scoping batch (Q&A #1), the resized size remembered between launches (Q&A #2), a simple subject
(Q&A #3). Codebase read directly (the questions chained: the size depends on the grid's layout,
which depends on the DPI). Proposed: a default size computed from the grid's metrics and two named
constants, the resized size saved at the end of each resize in logical pixels, every failure falling
back to the default. Three open questions: the position, the file, a reset item.

### Iteration 2 — 2026-10-08

Q&A #4–6 answered: the **size only** is remembered, never the position; it is saved in a shared
**`settings.json`** next to the exe (logical pixels, other keys kept); a **`Reset window size`** item
joins the settings menu — default size now, saved size removed. No open question left.

### Iteration 3 — 2026-10-08 — ✅ Implemented

Go given: **code, unit tests and documentation**, in a **worktree** (`.claude/worktrees/default-window-size`,
branch `feature/default-window-size`). Scope frozen on the design sections above.

### Iteration 4 — 2026-10-08 — 🧭 Implementation choices

- **`main` merged mid-run**, at the user's request, to take the frequent tab: one conflict in
  `UI/MainForm.cs` (two fields added side by side, both kept).
- **Sizing through `SetClientArea`**, not the `ClientSize` setter, which counts a caption the window
  does not have: the first check showed a 9th row, the window a caption (38 px) too tall. Closest
  workable variant of *client size = …*.
- **Search bar measured by its preferred height**: its `Height` still read the default 100 in
  `OnLoad`.
- **Rows measured on category rows** (cell 40): since the frequent tab, the window opens on the
  frequent section, whose captioned cells are taller (54) — at the top, the window does not show
  exactly 8 rows of that section.
- **Working-area clamp counts the invisible resize borders**: conservative, the visible window may
  stay ~16 px short of the full working area.
- **Move-only resize saves nothing**: the logical sizes at `OnResizeBegin` and `OnResizeEnd` are
  compared.
- **Menu order**: *Open app folder*, *Reset window size*, *Clear frequently used*.
- No rule broken.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 4 | 2026-10-08 | Default size, remembered size, *Reset window size*; checked at 125 % (16 columns × header + 8 rows, resize saved and reloaded, reset) |
| Unit tests | 3 | 2026-10-08 | No test project — manual checks only |
| README | 3 | 2026-10-08 | `README.md` / `README.fr.md` *Window* bullet; `RULES.md` § *Size* and the reset row |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | "6 rows of emojis" in height: how are they counted, given the section headers? | Header + 8 rows, in fact | 2026-10-08 |
| 2 | After the user resizes the window? | Remembered between launches | 2026-10-08 |
| 3 | Is the subject simple, or tricky / long? | Simple | 2026-10-08 |
| 4 | Is the window's position remembered too, or only its size? | The size only | 2026-10-08 |
| 5 | Where is the size saved: `window.json` or a shared `settings.json`, next to the exe? | `settings.json` | 2026-10-08 |
| 6 | A *Reset window size* item in the settings menu, or none? | The item in ⚙ | 2026-10-08 |

---

*Last updated: 2026-10-08*
