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
  `Bitmap`.
- **Measured** (2026-10-08, 1,644 emojis at 28 px): **7.9 ms per emoji**, 13 s for them all. Rebuilding the
  resources is only ~1.6 ms of it: the bulk is **DirectWrite's first draw of each colour glyph** in the process —
  ~5 ms the first time, ~0.9 ms for a glyph already drawn. That first-draw cost cannot be reused away; it can only
  be moved off the UI thread, or skipped by the disk cache.
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
- A failed draw releases the per-size resources: the next call rebuilds them.
- **Isolated DirectWrite factory**, one per renderer: the shared factory is one COM object per process, and its
  wrapper, created on the thread of the first renderer (the tray icon's, on the UI thread), fails from the
  pre-render thread with `E_NOINTERFACE`.
- Measured gain: 7.9 → 6.3 ms per emoji (see § Current Cost for why it is not more).

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
- **Start**: in the grid's constructor, at the emoji size of the DPI it assumes; checked again when its handle is
  created (the window may open on a monitor of another DPI) and on every DPI change — another size cancels the
  running pre-render and starts a new one.
- **Disposal**: a run's bitmaps are disposed exactly once, by whichever comes second between its thread finishing
  and its cancellation — never while the thread may still publish.
- **A failed emoji** stays green, and the atlas is then not written (it would be incomplete).
- **End of app**: the thread is cancelled, the bitmaps disposed with the grid.
- **Memory**: all the emojis stay in memory, as they already do once each has been seen — about 1,900 bitmaps,
  ~6 MB at 100 % scaling, ~13 MB at 150 %.

### Paint Before the Pre-Render Reaches an Emoji

**Never rendered on the UI thread.** A cell whose emoji is not ready yet is filled with **fluorescent green**
(`#39FF14`, inside the cell like the hover highlight), so a missing emoji is obvious rather than looking like an
empty slot.

- When bitmaps are published, the cache tells the grid on the UI thread (`SynchronizationContext.Post`), **coalesced**: one pending
  notification at most; the grid then invalidates itself and the green cells get their emoji.
- The hovered cell's highlight is drawn under the emoji as today; a not-ready hovered cell stays green.

---

## Disk Cache Next to the Exe

The pre-rendered emojis are saved in a subfolder of the exe's folder, and reloaded from it at the next launch
instead of being rendered again.

- **Folder**: `cache` in `AppContext.BaseDirectory` — kebab-case, one `FolderName` constant in the cache's class
  (`../CLAUDE.md` § Folder Names). Created on first write.
- **Files, per emoji size**: `emojis-{size}.png`, an **atlas** — every emoji in pre-render order (tab order, then
  grid order), on 64 columns, premultiplied colours saved as non-premultiplied PNG (~1.7 MB at 28 px, ~2.5 MB at
  35 px) — and
  `emojis-{size}.key`, a text file holding the **key** the atlas was built for.
- **Key**: the emoji size in pixels; the Segoe UI Emoji font file's size and last-write time
  (`%WINDIR%\Fonts\seguiemj.ttf`, so a Windows update changing the font invalidates it); a hash of the ordered emoji
  list (so an Emojibase update or a catalog change invalidates it); the renderer's parameters (font scale, raise,
  a format version). The key file is compared as a whole: any difference → the atlas is ignored.
- **Launch flow, on the pre-render thread**: key matches → the atlas is loaded and cut into one bitmap per emoji,
  published like rendered ones. Missing, mismatched or unreadable → everything is rendered (§ Background
  Pre-Rendering), then the atlas and its key are written.
- **Write**: to `.new` files in the same folder; the old key is deleted, then the atlas and the key are moved in
  place, the key last — a crash never leaves a key pointing at a half-written atlas.
- **Not writable** (an exe under `Program Files`, a read-only folder): every write error is swallowed — the app
  renders at every launch, nothing else changes. No fallback location.
- **DPI change**: the same flow at the new size; the files of every size already met are kept side by side.

---

## Documentation

- `RULES.md` § Categories and Insertion: the emojis are pre-rendered in the background at launch, cached in
  `cache\` next to the exe, a not-ready cell shown fluorescent green; `FormatVersion` bumped by a rendering change
  the key does not see; one renderer per thread, isolated DirectWrite factory.
- `README.md` / `README.fr.md`: the `cache` folder the app creates next to its exe, and that deleting it is safe.

---

## Test Impact

**No unit test**: the repository has no test project, and none is created for this work (user's decision). The
verification is manual, during the run, and written in this workfile:

| Behaviour to verify | How | Result (2026-10-08) |
|---|---|---|
| Time to pre-render every emoji, cold (no cache) | Temporary harness, not committed (`Stopwatch`) | ✅ first screen (90 emojis) 650 ms, all 1,644 in 11.2 s, atlas on disk at 11.3 s |
| Time to load them from the atlas, warm | Same | ✅ all 1,644 in **58 ms** |
| The atlas holds every emoji, in colour | Atlas PNG opened | ✅ |
| Warm launch, last tab (Symbols) clicked at once: no green cell | App launched, screenshot | ✅ |
| Cold launch, scrolled to the end at once: green cells, then filled | App launched, `WM_VSCROLL` + `PrintWindow` | ✅ green at 600 ms, filled later |
| A `cache` that cannot be written (a file named `cache` in its place): the app still works | App launched for 14 s, then closed | ✅ no crash, exit code 0, nothing written |
| A key change (another DPI) rebuilds the atlas at the new size | Manual | ⚠️ partly: a 125 % display wrote `emojis-35.*` while the harness wrote `emojis-28.*`, side by side; a live DPI change (window moved to another monitor) **not tested** |

---

## Open Questions

- [x] ~~Disk cache next to the exe: in this workfile, or left for later?~~ → In this workfile; silent fallback (no
  disk cache) when the folder cannot be written.
- [x] ~~An emoji painted before the pre-render reached it: synchronous render or blank cell?~~ → A cell filled with
  fluorescent green, so a missing emoji is obvious; drawn as soon as it is ready.
- [x] ~~No test project exists: create one, or keep the verification manual?~~ → Manual verification, timings
  written in this workfile.
- [ ] Out of scope, raised by the run: the cold pre-render takes ~11 s on one thread (DirectWrite's first draw of
  each glyph). Split it across several threads, each with its own renderer, to shorten the first launch — and the
  launches after a Windows update of the emoji font?

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

### Iteration 3 — 2026-10-08 — ✅ Implemented

Go given: code, tests (manual verification) and documentation, in a worktree
(`.claude/worktrees/fast-emoji-display`, branch `feature/fast-emoji-display`).

### Iteration 4 — 2026-10-08 — 🧭 Implementation choices

No rule broken. Choices the frozen design did not state:

- **Isolated DirectWrite factory** in every renderer — not in the design, found by the first real launch: the shared
  factory's wrapper, created on the UI thread by the tray icon's renderer, cannot be used from the pre-render thread
  (`E_NOINTERFACE`, the app crashed). The harness had missed it, having no tray icon.
- **Reusing the resources gains little** (7.9 → 6.3 ms per emoji): the measured bulk is DirectWrite's first draw of
  each glyph. Kept as designed; the cold pre-render takes ~11 s, the warm load 58 ms. Splitting the cold pre-render
  across threads is offered as an Open Question, not implemented (scope freeze).
- **Notification** through the UI thread's `SynchronizationContext` (captured when the grid creates the cache)
  rather than `BeginInvoke`: the grid's handle does not exist yet when the pre-render starts.
- **Start point**: the grid's constructor, re-checked at handle creation and on DPI changes, the cache's `Size`
  telling whether a new run is needed.
- **Run disposal**: exactly once, by whichever comes second between the thread finishing and the cancellation.
- **A failed emoji** stays green and the atlas is not written.
- **Atlas**: 64 columns; temporary files suffixed `.new`; the old key deleted before the moves.
- `EmojiRenderer.FontScale` / `RaiseScale` made **public**, read by the cache's key.
- **Measurements** were taken with a temporary harness in `Program.Main`, removed before every commit.
- **Not verified**: a live DPI change (window moved to a monitor of another scale).

### Iteration 5 — 2026-10-08 — ⚙️ Post-implementation — Merge with the search box

`main` had received the search box (`feature/search-box`) since the branch was created. Merging it into the branch
conflicted in `UI/EmojiGrid.cs` only. Beyond the conflict, Git silently kept the cache lookup on
`this.categories[section]`, while the grid now paints `this.sections` (the categories, or the search results): it
was moved to `this.sections`. Checked in the app: the categories and a search (`cat`) both show their emojis from
the cache. Then merged into `main`, the worktree and its branch removed.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 4 | 2026-10-08 | Renderer reuse, `EmojiBitmapCache`, grid wiring, isolated DirectWrite factory |
| Unit tests | 3, 4 | 2026-10-08 | None by decision (no test project): manual verification, see § Test Impact |
| Documentation (RULES, README × 2) | 3, 4 | 2026-10-08 | RULES § Categories and Insertion; README / README.fr § Features |

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
| 8 | Go for implementation? (scope, where) | Code, tests and docs — in a worktree | 2026-10-08 |

---

*Last updated: 2026-10-08*
