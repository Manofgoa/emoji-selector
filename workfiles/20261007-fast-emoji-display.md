# Fast Emoji Display

> Working document — make the emoji grid show instantly, the first scroll included.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The grid is slow **the first time** it scrolls over emojis not seen yet; once seen, they are in memory and
scrolling is fast (user's observation). Goal: **instant display** — the window and the whole grid ready as soon as
they appear. The app's own start may take longer for that: the emojis are **pre-rendered in the background**, and
**kept on disk** next to the exe so the next launches reload them instead of rendering them again.

Components: `Drawing/EmojiRenderer.cs` (colour rendering through Direct2D + DirectWrite), `UI/EmojiGrid.cs`
(paint, bitmap cache), `UI/MainForm.cs` (where the grid is created at launch), a new `Drawing/EmojiBitmapCache.cs`.

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

**Never rendered on the UI thread.** A cell whose emoji is not ready yet is filled with **fluorescent green**
(`#39FF14`, inside the cell like the hover highlight), so a missing emoji is obvious rather than looking like an
empty slot.

- When bitmaps are published, the cache tells the grid on the UI thread (`BeginInvoke`), **coalesced**: one pending
  notification at most; the grid then invalidates itself and the green cells get their emoji.
- The hovered cell's highlight is drawn under the emoji as today; a not-ready hovered cell stays green.

---

## Disk Cache Next to the Exe

The pre-rendered emojis are saved in a subfolder of the exe's folder, and reloaded from it at the next launch
instead of being rendered again.

- **Folder**: `cache` in `AppContext.BaseDirectory` — kebab-case, one `FolderName` constant in the cache's class
  (`../CLAUDE.md` § Folder Names). Created on first write.
- **Files, per emoji size**: `emojis-{size}.png`, an **atlas** — every emoji in pre-render order (tab order, then
  grid order), on a fixed number of columns, premultiplied colours saved as non-premultiplied PNG — and
  `emojis-{size}.key`, a text file holding the **key** the atlas was built for.
- **Key**: the emoji size in pixels; the Segoe UI Emoji font file's size and last-write time
  (`%WINDIR%\Fonts\seguiemj.ttf`, so a Windows update changing the font invalidates it); a hash of the ordered emoji
  list (so an Emojibase update or a catalog change invalidates it); the renderer's parameters (font scale, raise,
  a format version). The key file is compared as a whole: any difference → the atlas is ignored.
- **Launch flow, on the pre-render thread**: key matches → the atlas is loaded and cut into one bitmap per emoji,
  published like rendered ones. Missing, mismatched or unreadable → everything is rendered (§ Background
  Pre-Rendering), then the atlas and its key are written.
- **Write**: to temporary files in the same folder, then renamed over the old ones, the key last — a crash never
  leaves a key pointing at a half-written atlas.
- **Not writable** (an exe under `Program Files`, a read-only folder): every write error is swallowed — the app
  renders at every launch, nothing else changes. No fallback location.
- **DPI change**: the same flow at the new size; the files of every size already met are kept side by side.

---

## Documentation

- `RULES.md` § Categories and Insertion: the emojis are pre-rendered in the background at launch, cached in
  `cache\` next to the exe, a not-ready cell shown fluorescent green.
- `README.md` / `README.fr.md`: the `cache` folder the app creates next to its exe, and that deleting it is safe.

---

## Test Impact

**No unit test**: the repository has no test project, and none is created for this work (user's decision). The
verification is manual, during the run, and written in this workfile:

| Behaviour to verify | How | Create / Update |
|---|---|---|
| Time to pre-render every emoji, cold (no cache) | Timing measured in the run (`Stopwatch`, debug output) | — |
| Time to load them from the atlas, warm | Same | — |
| First scroll over every category, once the pre-render is done: no green cell, no hitch | Manual, app launched | — |
| A key change (another DPI) rebuilds the atlas at the new size | Manual | — |
| A read-only `cache` folder: the app still works | Manual | — |

---

## Open Questions

- [x] ~~Disk cache next to the exe: in this workfile, or left for later?~~ → In this workfile; silent fallback (no
  disk cache) when the folder cannot be written.
- [x] ~~An emoji painted before the pre-render reached it: synchronous render or blank cell?~~ → A cell filled with
  fluorescent green, so a missing emoji is obvious; drawn as soon as it is ready.
- [x] ~~No test project exists: create one, or keep the verification manual?~~ → Manual verification, timings
  written in this workfile.

---

## Design Iterations

### Iteration 1 — 2026-10-07

Scoping answers: the slowness shows on the first scroll only; the target is instant display, the app's start may
take longer; the accepted trade-off is background pre-rendering; the subject is expected to be straightforward
(single exploration pass). Exploration found the cost: a full Direct2D setup per emoji, done lazily inside
`OnPaint`. Proposed: reuse the renderer's per-size resources, and pre-render every emoji at launch on a background
thread. The user's disk-cache question is answered in its section and left open as to scope.

### Iteration 2 — 2026-10-07

Open questions answered. The **disk cache** joins the scope: a `cache` folder next to the exe, one PNG atlas and
one key file per emoji size, silently skipped when the folder cannot be written. A not-ready emoji is **never
rendered on the UI thread**: its cell is filled with fluorescent green so a missing emoji is obvious, then drawn
once the pre-render publishes it. No test project: the verification is manual, timings recorded here. A
Documentation section is added (RULES, README in both languages).

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| Documentation (RULES, README × 2) | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | When does the slowness show? (window opening, tab change, scrolling, app start) | The first scroll; afterwards the emojis are in memory and it is fast. Also asked: can a cache live in a subfolder of the exe's folder? | 2026-10-07 |
| 2 | What does "ultra fast" target? | Instant display; the app's start may take longer (pre-rendering) | 2026-10-07 |
| 3 | Which trade-offs are acceptable? (more memory, background pre-render, disk cache, other rendering tech) | Background pre-rendering | 2026-10-07 |
| 4 | Straightforward or tricky subject? | Straightforward | 2026-10-07 |
| 5 | Disk cache next to the exe: now or later? | In this workfile | 2026-10-07 |
| 6 | Emoji painted before the pre-render reached it: synchronous render or blank cell? | Blank cell with a fluorescent green background, so it is clear the emoji is missing | 2026-10-07 |
| 7 | Create a test project, or manual verification? | Manual verification | 2026-10-07 |
| 8 | Go for implementation? (scope, where) | | |

---

*Last updated: 2026-10-07*
