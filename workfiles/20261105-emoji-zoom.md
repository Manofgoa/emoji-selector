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
- **Range**: 70 % to 400 %, step 10 %, 100 % by default.

Components: `UI/MainForm.cs` (settings menu, setting, wiring), `UI/EmojiGrid.cs` (cell and emoji sizes, the wheel),
`UI/EmojiGridLayout.cs` (unchanged: it already takes the cell size as an input), `Drawing/EmojiBitmapCache.cs`
(pre-render per size), `UI/EmojiDetailsPanel.cs` (large emoji, panel height), `Data/SettingsFile.cs` (the key).

---

## The Setting

- **`Emoji zoom`**, a percentage: 70, 80, … 400 (`MainForm.MinZoom`, `MaxZoom`, `ZoomStep`, `DefaultZoom`).
  100 % = today's sizes.
- The nearest step to an integer (`MainForm.NearestZoom`): a tie goes to the step nearer 100 % — `85` → 90,
  `115` → 110, `450` → 400.
- **Saved** as `emojiZoom` in `settings.json`, the percentage (`SettingsFile.ReadEmojiZoom` / `WriteEmojiZoom`), at
  each change. Missing or not an integer → 100 %. An integer that is not a step (written by hand) → the **nearest**
  step, clamped to the range — the key left as it is until the next change, as `opacity` does.
- `Reset all settings…` deletes `settings.json`: back to 100 % at the restart, nothing to add to `AppReset`.
- `Reset window size` leaves it alone.

---

## Settings Menu

- A **`ToolStripControlHost`** hosting a `FlowLayoutPanel` — the label `Emoji zoom`, then a `UI/PercentUpDown.cs`
  box (a `NumericUpDown` showing `130%`, reading a typed value's digits; 70–400, increment 10) —, placed in the
  *details panel* section **right after `Highlight color…`** (before `Always show skin tone`), as the user asked.
- **Live**: each ▲▼ click, each arrow key / wheel notch inside the box applies the zoom at once; a typed value
  applies on Enter or when the box loses the focus. The menu **stays open** while the box is used.
- The box shows the zoom in use at each opening of the menu (Ctrl+wheel may have changed it).
- A typed value outside the range, or between two steps, is brought to the nearest step in the range
  (`NumericUpDown` clamps; the step rounding is ours).

---

## Ctrl+Wheel

- **Over the grid only** (`EmojiGrid.OnMouseWheel`): Ctrl held → one notch = one step up (wheel away) or down,
  clamped to the range; the grid does **not** scroll. Without Ctrl, the wheel scrolls as today.
- The grid raises an event (`ZoomRequested`: the steps and the mouse's position); `MainForm.SetEmojiZoom` applies
  and saves it — the same path as the menu's box.
- High-resolution wheels (deltas under 120) accumulate until a whole notch.
- **Anchor**: the emoji **under the mouse** keeps its **vertical** place on screen, as far as the scroll allows —
  the columns reflow, so it may move sideways. Over no emoji (a header, a gap), the point under the mouse keeps its
  relative place in the content.
- The menu's box keeps the **selection** at its place on screen, then scrolls it into view if needed.
- While a search is shown, the categories' saved scroll position is scaled with their height.
- **No keyboard shortcut** (Ctrl+`+` / Ctrl+`-` / Ctrl+`0`): not asked, out of scope.

---

## Zoom Indicator

- While the zoom changes — Ctrl+wheel or the menu's box, even at an end of the range —, the details panel shows the
  **zoom value** (`120%`) **under the tone swatch**, in the right column (the swatch under the copy button)
  (`EmojiDetailsPanel.ShowZoom`).
- **During the scroll**: the grid and the panel are painted at once after each change (`Update`) — Windows paints
  only once no message waits, and every notch brings one.
- It **disappears 2 sliding seconds** after the last change (a `System.Windows.Forms.Timer` restarted at each
  change); a change while it shows updates it and restarts the delay.
- **Its line is always reserved**: the right column's height (`RightColumnHeight`, so `HeightFor`) counts it, so
  the panel never changes height — nor the grid moves — when the value appears or goes.
- Swatch not shown (`Always show skin tone` off, the selection without variants): the value at the swatch's place.
- Drawn in the panel's text font, grey (`SystemColors.GrayText`, `Glass.GrayText` on Acrylic), centred under the
  swatch; not zoomed itself.
- Not shown at launch, nor when the window is shown: only on a change.

---

## What the Zoom Scales

| Element | Zoomed | Not zoomed |
|---|---|---|
| Grid | Cell size (`LogicalCellSize` 40), emoji size (`LogicalEmojiSize` 28), the captioned cell's emoji part | Header height, padding, sub-category labels and their gap, the use counts' font, the selection frame's width, the insertion bar |
| Details panel | The large emoji (`LogicalEmojiSize` 48) — so the panel's height (`HeightFor`) | The names, tags, flags, emoticons' font, copy button, tone swatch |
| Window | — | Its size: the window keeps its size, the grid gets more or fewer columns. The **default size** (16 columns × 8 rows, `Reset window size` included) is computed **at 100 %** whatever the zoom (`EmojiGrid.SizeFor`, `EmojiDetailsPanel.HeightFor` given the unzoomed sizes) |
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
  visited gets its own, reloaded at once the next time. **All kept**: no atlas deleted on a zoom change — the folder
  is disposable, `Reset all settings…` empties it.
- **Transition**: the **previous run is kept as a fallback**: a cell whose emoji the new run has not reached yet
  draws the previous size's bitmap **stretched** to the new size (slightly blurry for a moment). The fallback is
  released once the new run has finished. Fluorescent green stays for a cell no size has yet (first launch). A DPI
  change gets the same fallback.
- Several quick zoom changes (Ctrl+wheel notches): each starts a new run, the previous one stopped; the fallback is
  the previous run holding the **most bitmaps** — the last complete one survives a burst of notches.
- **On demand** — the large sizes: a size whose bitmaps, every emoji's, would exceed 100 MB (`CompleteMaxBytes`,
  about 82 px: 300 % at 100 % scaling, 200 % at 150 %) is not pre-rendered. A background thread renders what the grid
  asks for at each paint (`EmojiGrid.RequestVisibleBitmaps`): the cells in view, then the next screen, then the
  previous one — starting over as soon as the list changes. Bitmaps beyond 150 MB (`OnDemandMaxBytes`) no longer
  asked for are freed, on the UI thread. **No atlas** is written for those sizes.
- On demand, the **100 % size stands in**, complete: pre-rendered (its atlas usually on disk) when no complete run is
  at hand — none, an on-demand one, or one stopped before its end. Back to the 100 % size while that stand-in is
  still running, it becomes the run.
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

- [x] ~~Transition while re-rendering: previous size's bitmaps stretched, or fluorescent green?~~ → The previous
  size's bitmaps stretched, until the new run has the emoji; green only where no size has it yet.
- [x] ~~Anchor of Ctrl+wheel?~~ → The emoji under the mouse keeps its place on screen; the menu's box keeps the
  selection in view.
- [x] ~~Default window size at 100 % or at the zoom in use?~~ → At 100 %, whatever the zoom.
- [x] ~~Keyboard shortcuts Ctrl+`+` / Ctrl+`-` / Ctrl+`0`?~~ → No, out of scope.
- [x] ~~Menu label?~~ → `Emoji zoom`.
- [x] ~~Disk cache growth: keep every zoom level's atlas?~~ → Keep them all.
- [x] ~~Indicator's delay?~~ → 2 seconds after the last change.
- [x] ~~Indicator's room: reserved always, or overlapping?~~ → Reserved always: the panel never changes height for it.

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

### Iteration 2 — 2026-11-05

The six open questions answered, every proposal accepted: the previous size's bitmaps shown stretched while the new
size is pre-rendered (instead of fluorescent green); Ctrl+wheel anchored on the emoji under the mouse, the menu's
box keeping the selection in view; the default window size computed at 100 %; no keyboard shortcut; the label
`Emoji zoom`; every zoom level's atlas kept in `cache\`. Sections *Ctrl+Wheel*, *What the Zoom Scales* and
*Pre-render and Cache* updated.

### Iteration 3 — 2026-11-05

The user's request: while the zoom changes, show its value under the tone swatch, gone after a few seconds. New
section *Zoom Indicator*; two open questions (the delay, the room in the panel). The implementation itself will run
in a worktree, once the session implementing the "near" mode — working in the main checkout — has finished.

### Iteration 4 — 2026-11-05

The indicator's open questions answered: it disappears 2 seconds after the last change, and its line is always
reserved in the details panel's right column. Section *Zoom Indicator* updated.

### Iteration 5 — 2026-11-05 — ✅ Implemented

Go given: code, tests and documentation, in a worktree (`.claude/worktrees/emoji-zoom`, branch
`feature/emoji-zoom`), started once the "near" mode session had finished and been merged into `main`.

### Iteration 6 — 2026-11-05 — ⚙️ Post-implementation — Range 70–400 %, indicator during the scroll

The user, testing the check instance mid-run:
- The zoom's value showed only once Ctrl+wheel stopped: it must show **during** the scroll, 2 sliding seconds after
  the last notch. Cause: each notch lays the grid and the panel out again, and Windows paints only once the message
  queue is empty — the panel is now painted at once after each change.
- The range becomes **70 % → 400 %**, step 10 %.
- Asked how to draw the large sizes — at 400 % on a 150 % screen an emoji is 168 px: pre-rendering the ~3,700 emojis
  and variants would take ~420 MB, and the disk atlas would be an image GDI+ refuses. Answer: **visible first** — the
  small sizes pre-rendered completely as before; beyond, a background thread renders the cells in view first, then
  their neighbours, in a bounded set; a cell not ready shows the largest complete size, stretched; no disk atlas for
  those sizes.

### Iteration 7 — 2026-11-05 — 🧭 Implementation choices

- **On-demand threshold by memory, not by zoom**: "up to 200 %" depends on the screen's scale — an emoji at 200 % is
  56 px at 100 % scaling, 112 px at 200 %. A size is pre-rendered completely while every emoji's bitmap fits in
  100 MB (~82 px), on demand beyond; on demand keeps ~150 MB of bitmaps at most.
- **The stand-in on demand is the 100 % size**, not "the largest complete size": the cache does not know the zoom
  steps, and the 100 % atlas is the one almost always on disk. Of the previous runs, the one holding the most bitmaps
  stands in otherwise.
- **Indicator format** `130%`, without a space — the app's English UI writes `100%` in *Window opacity* too.
- **Anchor**: the emoji under the mouse keeps its vertical place; the columns reflow, so it can move sideways. The
  menu's box keeps the selection at its place on screen, then scrolls it into view.
- **A tie between two steps** (`85`, `115`, written by hand) goes to the step nearer 100 %.
- **The value shows even at an end of the range**, where Ctrl+wheel changes nothing: the user sees why.
- **`PercentUpDown`**, a small `NumericUpDown` subclass, for the `%` in the box.
- **Reset all settings…**' confirmation now names the emoji zoom among what is lost.
- **A check instance and the in-process checks**: Ctrl+wheel and the menu cannot be driven from a script; `MainForm`
  was run inside `pwsh -Sta` (PowerShell 7 on .NET 10) on a copy of the build, `SetEmojiZoom` called by reflection
  — written into RULES.md for the next agents.
- Noted, not changed (scope frozen): at 400 % the details panel's emoji is 192 logical pixels, and the panel takes
  most of a default-size window — the user chose "grid + panel".
- No rule broken.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | Iterations 5, 6 | 2026-11-05 | Setting, cache stand-in, grid zoom and Ctrl+wheel, details panel and indicator, menu box; then 70–400 %, on-demand rendering, paint during the scroll |
| Unit tests | 5 | 2026-11-05 | Not applicable: no test project in the repository. Checked by a launched instance at 150 % and by `MainForm` run in-process (nearest step, anchor, box, clamp, 400 % on demand, 70 %) |
| README | Iterations 5, 6 | 2026-11-05 | `README.md` / `README.fr.md` (*Zoom*, the gear's menu), `GLOSSARY.md` / `GLOSSARY.fr.md` (*Zoom*), `RULES.md` (*Emoji Zoom*, the table, the menu's sections, `settings.json`, the reset, the cache, the details panel) |

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
| 7 | The six open questions (transition, anchor, default window size, shortcuts, label, cache growth) | Every proposal accepted: stretched previous bitmaps, anchor on the emoji under the mouse, default size at 100 %, no shortcut, `Emoji zoom`, keep every atlas | 2026-11-05 |
| 8 | Go: scope and where? | Where: a worktree, once the session implementing the "near" mode has finished. Scope not given yet. Then a new request: the zoom indicator under the tone swatch | 2026-11-05 |
| 9 | The indicator's delay and its room in the details panel | 2 seconds; the line always reserved | 2026-11-05 |
| 10 | Go: scope? | Code, tests and documentation | 2026-11-05 |
| 11 | Up to 400 %: how to draw the large sizes — visible first, everything pre-rendered (~420 MB), or capped at 200 % and enlarged? | Visible first | 2026-11-05 |

---

*Last updated: 2026-11-05*
