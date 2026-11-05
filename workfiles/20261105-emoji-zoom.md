# Emoji Zoom

> Working document — a zoom on the emojis: a numeric box in the settings menu, Ctrl+wheel over the grid.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The emojis of the grid and the details panel's large emoji are drawn at a fixed logical size (28 px in a 40 px
cell, 48 px in the panel). The user wants to **zoom on the emojis only** — not on the whole interface: the texts,
the tabs, the search box, the headers and the sub-category labels keep their size.

- **Where it is set**: a numeric box with its ▲▼ arrows, in the settings menu, right under `Highlight color…`.
- **Ctrl+wheel** over the grid changes it too.
- **Range**: 80 % to 200 %, step 10 %, 100 % by default.

Components: `UI/MainForm.cs` (settings menu, setting, wiring), `UI/EmojiGrid.cs` (cell and emoji sizes, the wheel),
`UI/EmojiGridLayout.cs` (unchanged: it already takes the cell size as an input), `Drawing/EmojiBitmapCache.cs`
(pre-render per size), `UI/EmojiDetailsPanel.cs` (large emoji, panel height), `Data/SettingsFile.cs` (the key).

---

## The Setting

- **`Emoji zoom`**, a percentage: 80, 90, … 200 (`MainForm.ZoomPercents`, or min / max / step constants).
  100 % = today's sizes.
- **Saved** as `emojiZoom` in `settings.json`, the percentage (`SettingsFile.ReadEmojiZoom` / `WriteEmojiZoom`), at
  each change. Missing or not an integer → 100 %. An integer that is not a step (written by hand) → the **nearest**
  step, clamped to the range — the key left as it is until the next change, as `opacity` does.
- `Reset all settings…` deletes `settings.json`: back to 100 % at the restart, nothing to add to `AppReset`.
- `Reset window size` leaves it alone.

---

## Settings Menu

- A **`ToolStripControlHost`** hosting a `NumericUpDown` (80–200, increment 10, the `%` shown), labelled
  `Emoji zoom` — one menu row, the label then the box —, placed in the *details panel* section **right after
  `Highlight color…`** (before `Always show skin tone`), as the user asked.
- **Live**: each ▲▼ click, each arrow key / wheel notch inside the box applies the zoom at once; a typed value
  applies on Enter or when the box loses the focus. The menu **stays open** while the box is used.
- The box shows the zoom in use at each opening of the menu (Ctrl+wheel may have changed it).
- A typed value outside the range, or between two steps, is brought to the nearest step in the range
  (`NumericUpDown` clamps; the step rounding is ours).

---

## Ctrl+Wheel

- **Over the grid only** (`EmojiGrid.OnMouseWheel`): Ctrl held → one notch = one step up (wheel away) or down,
  clamped to the range; the grid does **not** scroll. Without Ctrl, the wheel scrolls as today.
- The grid raises an event (`ZoomRequested`, the new percentage); `MainForm` applies and saves it — the same path as
  the menu's box.
- High-resolution wheels (deltas under 120) accumulate until a whole notch.

---

## What the Zoom Scales

| Element | Zoomed | Not zoomed |
|---|---|---|
| Grid | Cell size (`LogicalCellSize` 40), emoji size (`LogicalEmojiSize` 28), the captioned cell's emoji part | Header height, padding, sub-category labels and their gap, the use counts' font, the selection frame's width, the insertion bar |
| Details panel | The large emoji (`LogicalEmojiSize` 48) — so the panel's height (`HeightFor`) | The names, tags, flags, emoticons' font, copy button, tone swatch |
| Window | — | Its size: the window keeps its size, the grid gets more or fewer columns |
| Tray icon, tabs, search box | — | Everything |

- The zoomed sizes are rounded to whole device pixels: `LogicalToDeviceUnits(logical) × zoom / 100`.
- A captioned cell (frequent section): its height is the zoomed emoji part plus the caption's unzoomed height.
- The details panel growing takes its room from the grid (the window keeps its size); the window's minimum height
  follows the panel's height, as today.
- The frequent section's limit stays 3 rows × the columns at the current width.

---

## Pre-render and Cache

- A zoom change is a new **emoji size**: the grid's `EnsureBitmapSize` starts a pre-render at that size, exactly
  like a DPI change. The disk cache already keeps one atlas per size (`cache\emojis-{size}.png`): each zoom level
  visited gets its own, reloaded at once the next time.
- Today `EmojiBitmapCache.Start` **drops the previous size's bitmaps**: until the new ones are ready, every cell is
  fluorescent green. See Open Questions for the transition.
- The details panel renders its emoji on demand with its own renderer: no cache involved.

---

## Test Impact

The repository has **no test project**: nothing to create or update. The behaviours below are checked by reflection
on the built dll and by launching the app, as the other features' checks do (RULES.md).

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project in the repository) | — | — |

---

## Open Questions

- [ ] **Transition while re-rendering**: on a zoom change, until the new size is pre-rendered, show the **previous
  size's bitmaps stretched** (slightly blurry for a moment) instead of fluorescent green? Proposal: yes — the
  previous run kept as a fallback until the new one has the emoji; green stays for a cell no size has yet.
- [ ] **Anchor**: Ctrl+wheel keeps **the emoji under the mouse** at its place on screen (as far as the scroll
  allows), like an image viewer? Proposal: yes. The menu's box keeps the **selection** in view instead.
- [ ] **Default window size** (16 columns × 8 rows, also used by `Reset window size`): computed at 100 % whatever
  the zoom, or at the zoom in use (at 200 % the default window would be twice as wide and tall)? Proposal: at 100 %.
- [ ] **Keyboard shortcuts** Ctrl+`+` / Ctrl+`-` / Ctrl+`0` (reset to 100 %)? Proposal: no, out of scope — not
  asked.
- [ ] **Menu label**: `Emoji zoom` (the box showing `100 %`)? Proposal: yes.
- [ ] **Disk cache growth**: 13 zoom levels × each DPI → up to 13 atlases per monitor scale in `cache\` (a few MB
  each at 200 %). Keep them all, or keep only the atlases of the current zoom? Proposal: keep them all — the folder
  is disposable and `Reset all settings…` empties it.

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-05

Initial design from the user's request (zoom on the emojis only, a numeric box with up/down arrows under the colour
selector, Ctrl+wheel) and the scoping answers: the box in the settings menu right under `Highlight color…`, the grid
and the details panel's large emoji zoomed, Ctrl+wheel over the grid only, 80–200 % by steps of 10. Codebase
findings: the grid's cell and emoji sizes are two constants scaled by the DPI, the layout takes the cell size as an
input, the bitmap cache already pre-renders and stores per size — a zoom change is handled like a DPI change. Six
open questions with proposals.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable: no test project in the repository |
| README | | | `README.md` / `README.fr.md`, `RULES.md` (*Window and Tray Icon* table, settings menu sections, `settings.json` keys), `GLOSSARY.md` / `GLOSSARY.fr.md` (*Zoom*) |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | "Under the colour selector": which one — the settings menu under `Highlight color…`, the details panel under the tone swatch, the menu's *window* section? | Asked back whether "Menu" meant a list of fixed values (80 to 200 by 10) — clarified: a real numeric box hosted in the menu | 2026-11-05 |
| 2 | Which emojis does the zoom apply to — the grid only, or the grid and the details panel's large emoji? | Grid + details panel | 2026-11-05 |
| 3 | Where does Ctrl+wheel change the zoom — over the grid, or anywhere in the window? | Over the grid | 2026-11-05 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — a single exploration pass | 2026-11-05 |
| 5 | Where the numeric box goes (clarified: a real `NumericUpDown` in the menu) | Settings menu, under `Highlight color…` | 2026-11-05 |
| 6 | Range and step | 80 → 200 %, step 10 | 2026-11-05 |
| 7 | The six open questions (transition, anchor, default window size, shortcuts, label, cache growth) | | 2026-11-05 |

---

*Last updated: 2026-11-05*
