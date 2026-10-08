# Tray Click Placement

> Working document — the tray icon's click, and the launch, show the window at the bottom right of
> the monitor holding the mouse pointer, like Windows' own flyouts.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the window is **centred** at launch (`StartPosition = CenterScreen`), and the tray icon's
click **never moves it** (RULES.md § Shortcut, *Placement*): hidden, it comes back where it was.
Only Win+; places it, under the text cursor.

The request: a click on the tray icon shows the window **on the monitor where the mouse is**, in
its **bottom-right corner** — the place Windows gives its own notification-area flyouts (network,
sound, calendar).

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
  it — the shadow excluded) sits at the **bottom right** of the working area, a **margin** away from
  its right and bottom edges, like Windows' flyouts.
  - The margin: **12 logical pixels** (`WindowPlacement.CornerMargin`), scaled to the window's DPI
    like `WindowPlacement.Gap`.
  - A frame larger than the working area (minus the margins) keeps its **top-left inside it** —
    the same clamping rule as `WindowPlacement.Place`.
- **Placed twice**, like Win+; does it: once before `Show` (no flash at the old place), once after,
  when the frame can be read and a move to a monitor of another DPI has resized the window.
- **Size**: unchanged — the remembered size, or the default one (RULES.md § Size). Only the
  position is computed; it is still never saved.

## When the Window Is Placed

| Event | Today | Planned |
|---|---|---|
| Launch | Centred on the main monitor | **Bottom right** of the mouse's monitor |
| Tray click, window **hidden** | Shown where it was | **Bottom right** of the mouse's monitor, then shown |
| Tray click, window shown but **covered** | Brought to the front where it is | Unchanged |
| Tray click, window **in front** | Hidden | Unchanged |
| Win+; / Win+. | Under the text cursor | Unchanged |
| The user moves the window | Stays there | Stays there — until the next hidden → shown tray click |

---

## Documentation

| File | Change |
|---|---|
| `RULES.md` § Shortcut, *Placement* | "The tray icon's click never moves the window" → its hidden → shown click places it in the corner |
| `RULES.md` § Window and Tray Icon | Tray icon row: hidden → shown at the bottom right of the mouse's monitor |
| `RULES.md` § Size | "centred at launch" → bottom right at launch |
| `README.md` / `README.fr.md` | Tray icon bullet: where the window appears |
| `GLOSSARY.md` / `GLOSSARY.fr.md` | No new term expected |

---

## Test Impact

No test project exists, and no earlier workfile created one. The new corner computation in
`WindowPlacement` is pure and could be unit-tested — whether to create a test project for it is
Open Question 4. Until it is settled, the checks are by script on the built exe:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| Launch → the frame at the bottom right of the mouse's monitor, 12 logical px from both edges | — (script check) | — |
| Tray click while hidden, pointer on another monitor → the window moves there, bottom right | — (script check) | — |
| Tray click while covered → brought to the front, not moved | — (script check) | — |
| Win+; still places the window under the text cursor | — (script check) | — |

---

## Open Questions

- [ ] **Taskbar not at the bottom** (Windows 10, or Windows 11 with the taskbar moved): the window
  goes to the bottom right of the working area whatever the taskbar's edge, or to the corner next
  to the notification area (top right with a taskbar on top, bottom left with one on the left)?
- [ ] **`Reset window size`** keeps the top-left corner: from the bottom-right corner, a larger
  default size would push the window past the taskbar and the screen's edge. Keep it as is, or
  keep the window inside the working area (moved up / left only as far as needed)?
- [ ] **Second launch** (workfile `20261008-start-with-windows.md`, being implemented in its own
  worktree): a second launch shows the first instance's window "as the tray icon's click does it
  when hidden or covered". Once both are merged, does a hidden window shown that way go to the
  bottom right of the mouse's monitor too?
- [ ] **Unit tests**: create a test project to pin the corner computation of `WindowPlacement`, or
  keep the script checks only, as the earlier workfiles did?

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
| 5 | Taskbar not at the bottom: bottom right always, or the corner next to the notification area? | | |
| 6 | `Reset window size` from the corner: kept as is, or kept inside the working area? | | |
| 7 | Second launch (start-with-windows): a hidden window shown that way goes to the corner too? | | |
| 8 | Unit tests: create a test project for the corner computation, or script checks only? | | |

---

*Last updated: 2026-10-08*
