# Fast Emoji Display

> Working document — make the emoji grid show instantly, the first scroll included.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The grid is slow **the first time** it scrolls over emojis not seen yet; once seen, they are in memory and
scrolling is fast (user's observation). Goal: **instant display** — the window and the whole grid ready as soon as
they appear. The app's own start may take longer for that: the emojis are **pre-rendered in the background**.

Components: `Drawing/EmojiRenderer.cs` (colour rendering through Direct2D + DirectWrite), `UI/EmojiGrid.cs`
(paint, bitmap cache), `UI/MainForm.cs` (where the grid is created at launch).

---

## Current Cost

What the code does today (read-only exploration, 2026-10-07):

- `EmojiGrid.GetBitmap` renders an emoji **lazily, on the UI thread, inside `OnPaint`**, the first time its cell
  becomes visible, then keeps it in `bitmaps` (one `Bitmap` per emoji, at the current DPI's emoji size).
- `EmojiRenderer.Render` builds **everything from scratch for every emoji**: a WIC bitmap, a WIC render target, a
  DirectWrite text format, a brush — then draws, copies the pixels to a `byte[]`, and copies them again into a GDI+
  `Bitmap`. The render target and the text format are the expensive part, and they are identical for every emoji of
  a given size.
- A first scroll brings a screenful of new cells (~80–100 at the default window size): that many full renders in a
  single paint — the hitch the user sees. Clicking a far tab does the same.
- A DPI change clears the cache: everything is rendered again, lazily.

---

## Rendering

**Reuse the per-size resources.** `EmojiRenderer` keeps, for the size it was last asked for, one WIC bitmap, one
render target, one text format and one brush; `Render` then only clears, draws and copies the pixels. A call with
another size (DPI change) rebuilds them.

- The pixels are copied **straight into the locked `Bitmap`** (`CopyPixels` into `BitmapData.Scan0` with its
  stride), dropping the intermediate `byte[]`.
- Output unchanged: same font scale, same raise, same premultiplied BGRA, same grayscale antialiasing.

---

## Background Pre-Rendering

**Every emoji is rendered at launch, on a background thread**, before the user opens the window.

- **Owner**: a new `Drawing/EmojiBitmapCache` holds the bitmaps (moved out of `EmojiGrid`) and the pre-render.
  `EmojiGrid` asks it for an emoji's bitmap; it no longer renders anything itself.
- **Thread**: one dedicated background thread (`IsBackground = true`, below-normal priority), with **its own
  `EmojiRenderer`** — the renderer is single-threaded (single-threaded Direct2D factory); each thread has its own.
- **Order**: the categories in tab order, each in grid order — the first screen (Smileys & People) is ready first.
- **Hand-over**: each finished `Bitmap` is published to a concurrent dictionary keyed by emoji text. Once published,
  only the UI thread touches it.
- **Size**: the emoji size of the grid's DPI at launch. A **DPI change** cancels the running pre-render, disposes
  the bitmaps, and starts a new one at the new size.
- **End of app**: the thread is cancelled, the bitmaps disposed with the grid.
- **Memory**: all the emojis stay in memory, as they already do once each has been seen — about 1,900 bitmaps,
  ~6 MB at 100 % scaling, ~13 MB at 150 %.

### Paint Before the Pre-Render Reaches an Emoji

{See Open Questions — synchronous fallback or blank cell.}

---

## Disk Cache Next to the Exe

The user asked whether a cache can live in a subfolder of the exe's folder. Feasible:

- A kebab-case folder (`cache`, one `FolderName` constant — `../CLAUDE.md` § Folder Names) next to the exe, one
  atlas image per emoji size, keyed by: the emoji size in pixels, the Segoe UI Emoji font file's version, the
  Emojibase data version, the rendering parameters. Any key change → re-rendered and rewritten.
- Limits: an exe installed under `Program Files` cannot write next to itself → no disk cache there, silently (or a
  fallback under `%LOCALAPPDATA%`). A Windows update changing the font invalidates it.
- Gain: it only shortens the **first second or two after launch** — the time the background pre-render needs.
  Once the pre-render is done, the disk cache brings nothing more.

{In scope or not: see Open Questions.}

---

## Test Impact

The repository has **no test project** today.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| {See Open Questions — whether to create a test project} | | |

---

## Open Questions

- [ ] Disk cache next to the exe: in this workfile, or left for later (a backlog row) once the background
  pre-render's real duration is measured?
- [ ] An emoji painted before the pre-render reached it: rendered synchronously on the UI thread (never a blank
  cell, a small hitch possible in the first instants) or left blank and drawn as soon as it is ready (never a hitch)?
- [ ] No test project exists: create one for this work (e.g. the pre-render order, the cache's hand-over), or keep
  the verification manual (timings measured during the run and written in this workfile)?

---

## Design Iterations

### Iteration 1 — 2026-10-07

Scoping answers: the slowness shows on the first scroll only; the target is instant display, the app's start may
take longer; the accepted trade-off is background pre-rendering; the subject is expected to be straightforward
(single exploration pass). Exploration found the cost: a full Direct2D setup per emoji, done lazily inside
`OnPaint`. Proposed: reuse the renderer's per-size resources, and pre-render every emoji at launch on a background
thread. The user's disk-cache question is answered in its section and left open as to scope.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | When does the slowness show? (window opening, tab change, scrolling, app start) | The first scroll; afterwards the emojis are in memory and it is fast. Also asked: can a cache live in a subfolder of the exe's folder? | 2026-10-07 |
| 2 | What does "ultra fast" target? | Instant display; the app's start may take longer (pre-rendering) | 2026-10-07 |
| 3 | Which trade-offs are acceptable? (more memory, background pre-render, disk cache, other rendering tech) | Background pre-rendering | 2026-10-07 |
| 4 | Straightforward or tricky subject? | Straightforward | 2026-10-07 |
| 5 | Disk cache next to the exe: now or later? | | |
| 6 | Emoji painted before the pre-render reached it: synchronous render or blank cell? | | |
| 7 | Create a test project, or manual verification? | | |

---

*Last updated: 2026-10-07*
