# Tray Click Placement

> Working document — the tray icon's click, and the launch, show the window in the corner of the
> monitor holding the mouse pointer — bottom right, next to the notification area — like Windows'
> own flyouts.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the window is **centred** at launch (`StartPosition = CenterScreen`), and the tray icon's
click **never moves it** (RULES.md § Shortcut, *Placement*): hidden, it comes back where it was.
Only Win+; places it, under the text cursor.

The request: a click on the tray icon shows the window **on the monitor where the mouse is**, in
its **bottom-right corner** — the place Windows gives its own notification-area flyouts (network,
sound, calendar); with a taskbar moved to another edge, the corner next to the notification area.

Components involved:

| Component | Role today | Change |
|---|---|---|
| `UI/WindowPlacement.cs` | Pure computation: the frame's top-left under / above the text cursor | + the bottom-right corner of a working area |
| `UI/MainForm.cs` — `OnTrayIconClicked` | Hidden → `Show` + `Activate`; covered → the same; in front → `Hide` | Hidden → placed in the corner first |
| `UI/MainForm.cs` — `PlaceAt` | Measures the frame margins, moves the visible frame against an anchor | Shared by both placements |
| `UI/MainForm.cs` — constructor / `OnLoad` | `CenterScreen`, sized in `OnLoad` before the centring | Placed in the corner instead |

---

## Placement

- **The monitor**: the one holding the **mouse pointer** (`Cursor.Position`) when the placement
  runs — `Screen.FromPoint`, its **working area** (the taskbar excluded).
  - On a click, the pointer is on the icon: on Windows 11 only the main taskbar holds the
    notification area, so it is that monitor. A keyboard activation of the icon (Win+B, then
    Enter) uses the pointer wherever it is.
- **The corner**: the window's **visible frame** (`DWMWA_EXTENDED_FRAME_BOUNDS`, as `PlaceAt` reads
  it — the shadow excluded) sits in the **corner next to the notification area** of that monitor's
  working area, a **margin** away from its two edges, like Windows' flyouts.
  - The margin: **12 logical pixels** (`WindowPlacement.CornerMargin`), scaled to the window's DPI
    like `WindowPlacement.Gap`.
- **Which corner** — the notification area sits at the end of the taskbar, so the corner follows
  the taskbar's edge on that monitor:

  | Taskbar | Corner |
  |---|---|
  | Bottom (Windows 11's only choice, Windows 10's default) | Bottom right |
  | Top | Top right |
  | Left | Bottom left |
  | Right | Bottom right |
  | None seen — auto-hidden, or no taskbar on that monitor | Bottom right |

  - The edge is read from the monitor itself: the side where its **working area is shorter than
    its bounds** (`Screen.Bounds` vs `Screen.WorkingArea`) — the widest gap when several sides
    differ (another app bar docked). No difference → bottom right. A pure computation in
    `WindowPlacement`, both rectangles given.
  - A frame larger than the working area (minus the margins) keeps its **top-left inside it** —
    the same clamping rule as `WindowPlacement.Place`.
- **Placed twice**, like Win+; does it: once before `Show` (no flash at the old place), once after,
  when the frame can be read and a move to a monitor of another DPI has resized the window.
- **Size**: unchanged — the remembered size, or the default one (RULES.md § Size). Only the
  position is computed; it is still never saved.

## When the Window Is Placed

| Event | Today | Planned |
|---|---|---|
| Launch | Centred on the main monitor | **Corner** of the mouse's monitor |
| Tray click, window **hidden** | Shown where it was | **Corner** of the mouse's monitor, then shown |
| Second launch, window **hidden** (start-with-windows workfile, once merged) | — | **Corner** of the mouse's monitor, like the tray click |
| `Reset window size` | Top-left corner kept, the window may leave the working area | Top-left corner kept, then moved up / left **only as far as needed** to stay inside the working area of its monitor |
| Tray click, window shown but **covered** | Brought to the front where it is | Unchanged |
| Tray click, window **in front** | Hidden | Unchanged |
| Win+; / Win+. | Under the text cursor | Unchanged |
| The user moves the window | Stays there | Stays there — until the next hidden → shown tray click |

- **One show path**: the tray click's "hidden → placed in the corner, shown, activated" lives in
  one `MainForm` method, so the second launch of `20261008-start-with-windows.md` calls it rather
  than repeating it. That workfile is implemented in its own worktree, not merged: this run does
  not touch it. Whichever branch is merged second makes the second launch's show go through that
  method (a hidden window → the corner; covered → brought to the front, not moved).
- **`Reset window size`** clamps the **visible frame** into the working area of the monitor the
  window is on (`Screen.FromHandle`), the same frame measure as `PlaceAt`; a window already inside
  does not move.

---

## Documentation

| File | Change |
|---|---|
| `RULES.md` § Shortcut, *Placement* | "The tray icon's click never moves the window" → its hidden → shown click places it in the corner |
| `RULES.md` § Window and Tray Icon | Tray icon row: hidden → shown in the corner of the mouse's monitor; `Reset window size` row: kept inside the working area |
| `RULES.md` § Size | "centred at launch" → in the corner at launch |
| `README.md` / `README.fr.md` | Tray icon bullet: where the window appears |
| `GLOSSARY.md` / `GLOSSARY.fr.md` | No new term expected |

---

## Test Impact

No test project exists, and none is created (Q&A 8): the corner computation of `WindowPlacement`
is pure, but it is checked like the earlier workfiles' work — **by script on the built exe**, and
for the taskbar's edge, by calling the pure methods by reflection on the built dll with made-up
rectangles. **No test file is created or updated.**

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| Launch → the frame in the bottom-right corner of the mouse's monitor, 12 logical px from both edges | — (script check) | — |
| Tray click while hidden, pointer on another monitor → the window moves there, in its corner | — (script check) | — |
| Tray click while covered → brought to the front, not moved | — (script check) | — |
| Taskbar on the top / left / right / none → top right / bottom left / bottom right / bottom right | — (reflection check) | — |
| `Reset window size` from the corner → the frame stays inside the working area | — (script check) | — |
| Win+; still places the window under the text cursor | — (script check) | — |

---

## Open Questions

- [x] ~~**Taskbar not at the bottom** (Windows 10, or Windows 11 with the taskbar moved): the window
  goes to the bottom right of the working area whatever the taskbar's edge, or to the corner next
  to the notification area (top right with a taskbar on top, bottom left with one on the left)?~~
  → The corner next to the notification area, following the taskbar's edge (Q&A 5)
- [x] ~~**`Reset window size`** keeps the top-left corner: from the bottom-right corner, a larger
  default size would push the window past the taskbar and the screen's edge. Keep it as is, or
  keep the window inside the working area (moved up / left only as far as needed)?~~ → Kept inside
  the working area, moved only as far as needed (Q&A 6)
- [x] ~~**Second launch** (workfile `20261008-start-with-windows.md`, being implemented in its own
  worktree): a second launch shows the first instance's window "as the tray icon's click does it
  when hidden or covered". Once both are merged, does a hidden window shown that way go to the
  bottom right of the mouse's monitor too?~~ → Yes, like the tray click: one show path, wired by
  whichever branch is merged second (Q&A 7)
- [x] ~~**Unit tests**: create a test project to pin the corner computation of `WindowPlacement`, or
  keep the script checks only, as the earlier workfiles did?~~ → Script checks only (Q&A 8)

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-08

Initial design from the request and the scoping answers (Q&A 1–4): a hidden → shown tray click and
the launch place the window's visible frame at the bottom right of the working area of the
monitor holding the mouse pointer, 12 logical pixels from its right and bottom edges, like
Windows' flyouts. A covered window is only brought to the front; Win+; is unchanged. The corner is
a new pure method of `WindowPlacement`; `PlaceAt`'s frame measuring is shared. Four questions
left open: the taskbar's edge, `Reset window size`, the second launch of the start-with-windows
workfile, unit tests.

### Iteration 2 — 2026-10-08

Open questions answered (Q&A 5–8):

- The corner is no longer always the bottom right: it is the one **next to the notification
  area**, following the taskbar's edge on the mouse's monitor, read from the gap between the
  monitor's bounds and its working area (bottom / right / none → bottom right, top → top right,
  left → bottom left).
- `Reset window size` now keeps the visible frame inside the working area of its monitor, moving
  the window up / left only as far as needed.
- The second launch of the start-with-windows workfile shows a hidden window in the corner too:
  the tray's show lives in one method, and whichever branch is merged second wires the second
  launch to it. This run does not touch that workfile's branch.
- No test project: script and reflection checks only.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |
| RULES.md / GLOSSARY | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | When does a tray click place the window at the bottom right of the mouse's monitor? | Only hidden → shown; a covered window is brought to the front where it is | 2026-10-08 |
| 2 | How far from the corner? | Like Windows' flyouts: inside the working area, a small margin from the right edge and the taskbar | 2026-10-08 |
| 3 | The window is centred at launch: does that change? | Yes: at launch too, bottom right of the mouse's monitor | 2026-10-08 |
| 4 | Exploration depth? | Straightforward: a single scout pass | 2026-10-08 |
| 5 | Taskbar not at the bottom: bottom right always, or the corner next to the notification area? | The corner next to the notification area | 2026-10-08 |
| 6 | `Reset window size` from the corner: kept as is, or kept inside the working area? | Kept inside the working area | 2026-10-08 |
| 7 | Second launch (start-with-windows): a hidden window shown that way goes to the corner too? | Yes, like the tray click | 2026-10-08 |
| 8 | Unit tests: create a test project for the corner computation, or script checks only? | Script checks only | 2026-10-08 |

---

*Last updated: 2026-10-08*
