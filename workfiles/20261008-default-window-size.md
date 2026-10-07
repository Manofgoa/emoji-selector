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
| The size saved when the user resizes, reloaded at the next launch | Where the window appears (Win+; under the text cursor, centred at launch — unchanged) |
| A saved size that does not fit the screen, or cannot be read → handled without error | The grid's metrics themselves (cell 40, header 32, padding 8) |

---

## Current State (codebase)

| Fact | Where |
|---|---|
| `ClientSize = new Size(440, 450)`, `StartPosition = CenterScreen`, `AutoScaleMode.Dpi` from 96 DPI | `UI/MainForm.cs` constructor |
| `MinimumSize` = the tab strip's minimum width + side borders, 240 high | `UI/MainForm.cs` |
| Client area, top to bottom: search bar (auto-sized), tab strip, grid (fills the rest) | `UI/MainForm.cs` |
| Grid metrics, logical pixels: cell **40**, header **32**, side padding **8**; a vertical scroll bar docked right | `UI/EmojiGrid.cs` |
| Columns = `(grid width − scroll bar − 2 × padding) / cell`; the first header sits at y = 0 (no top padding) | `UI/EmojiGridLayout.cs` |
| Nothing is persisted about the window; files next to the exe: `cache\` (emoji atlases), soon `usage.json` (frequent tab, being designed) | `Drawing/EmojiBitmapCache.cs`, `workfiles/20261008-frequent-tab.md` |
| No test project: changes are checked by hand | `CONTRIBUTING.md` |

---

## Default Size

Computed, not hard-coded: the numbers follow the grid's metrics and the DPI.

- **Grid width** = `16 × cell + 2 × padding + scroll bar width` → at 96 DPI: 16 × 40 + 2 × 8 + the
  system scroll bar (~17) ≈ **673** logical pixels.
- **Grid height** = `header + 8 × cell` → at 96 DPI: 32 + 8 × 40 = **352**.
- **Client size** = grid width × (search bar height + tab strip height + grid height), the two bars
  measured once laid out.
- The column and row counts live in **two named constants** (`DefaultColumns = 16`,
  `DefaultRows = 8`), next to the computation.
- At another DPI, every term is scaled the same way: 16 columns and header + 8 rows at any scale.
- Larger than the monitor's working area (small screen, high scale) → reduced to fit it.

---

## Remembered Size

- **Saved**: the window's size when the user **finishes a resize** (`ResizeEnd`) — not at exit, since
  an exit by Windows shutting down or the Task Manager may never run the app's code.
- **Stored in logical pixels** (96 DPI): reloaded on a monitor of another scale, the window holds the
  same number of columns and rows.
- **Reloaded** at launch, in place of the default size.
- **Fallback** — no file, unreadable or invalid content, a folder that cannot be written → the
  default size, never an error (like `cache\` and `usage.json`).
- A saved size smaller than `MinimumSize` → `MinimumSize`; larger than the working area → reduced to
  fit it.
- Where and in which file: see *Open Questions*.

---

## Documentation Impact

| File | Change |
|---|---|
| `RULES.md` / *Window and Tray Icon* | The default size (16 columns, header + 8 rows, computed), the remembered size (when saved, the file, the fallbacks) |
| `README.md` / `README.fr.md` | *Window* bullet: opens 16 emojis wide and 8 rows high; remembers the size it is resized to |

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

---

## Open Questions

- [x] ~~How are the 8 rows counted, given the section headers?~~ → The window opened at the top of a
  category shows its header, then 8 full rows
- [x] ~~What happens after the user resizes?~~ → The size is remembered between launches
- [ ] Is the window's **position** remembered too, or only its size?
- [ ] Where is the size saved: its own `window.json` next to the exe (like `usage.json`), or a
  shared `settings.json` next to the exe, ready for later settings?
- [ ] A way back to the default size — a *Reset window size* item in the settings menu (next to
  *Open app folder*) — or none?

---

## Design Iterations

### Iteration 1 — 2026-10-08

Request: default size of 16 emoji columns, height of "6" rows — revised to **header + 8 rows** in
the scoping batch (Q&A #1), the resized size remembered between launches (Q&A #2), a simple subject
(Q&A #3). Codebase read directly (the questions chained: the size depends on the grid's layout,
which depends on the DPI). Proposed: a default size computed from the grid's metrics and two named
constants, the resized size saved at the end of each resize in logical pixels, every failure falling
back to the default. Three open questions: the position, the file, a reset item.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project — manual checks only |
| README | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | "6 rows of emojis" in height: how are they counted, given the section headers? | Header + 8 rows, in fact | 2026-10-08 |
| 2 | After the user resizes the window? | Remembered between launches | 2026-10-08 |
| 3 | Is the subject simple, or tricky / long? | Simple | 2026-10-08 |
| 4 | Is the window's position remembered too, or only its size? | | 2026-10-08 |
| 5 | Where is the size saved: `window.json` or a shared `settings.json`, next to the exe? | | 2026-10-08 |
| 6 | A *Reset window size* item in the settings menu, or none? | | 2026-10-08 |

---

*Last updated: 2026-10-08*
