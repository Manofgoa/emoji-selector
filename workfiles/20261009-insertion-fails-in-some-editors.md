# Insertion Fails in Some Editors

> Working document — bug: a chosen emoji is inserted into Chrome, but nothing reaches the prompt box of the
> Claude desktop app (Code tab).
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Choosing an emoji (click, Enter) inserts it into the **previous window** (`MainForm.InsertEmoji`,
`Input/EmojiInserter.cs`). It works in Chrome; in the Claude desktop app's conversation prompt box,
**nothing is inserted**: the window hides, Claude comes back in front, no character appears. No other app
has been tested yet.

Constraint set by the user: **no clipboard**, not even as a fallback (RULES.md § Categories and Insertion
keeps "never through the clipboard").

---

## Current Insertion Sequence

`MainForm.InsertEmoji` (and `InsertKeepingOpen`, Ctrl+Enter / Ctrl+click):

1. `EmojiInserter.Activate(target)` — `ShowWindow(SW_RESTORE)` when minimized, then `SetForegroundWindow`.
2. `this.Hide()` (not in keep-open).
3. `EmojiInserter.Type(text)` — **right away**, in the same UI-thread call: one `SendInput` holding a
   `KEYEVENTF_UNICODE` key-down / key-up pair per UTF-16 code unit.

Nothing waits for the target to have **received the activation** and **given the keyboard focus back** to
its text field before the characters are sent.

---

## Findings (scout pass)

| Checked | Result |
|---|---|
| Elevation (UIPI) | ❌ not the cause: the Claude window's process (`WindowsApps\Claude_…\app\claude.exe`) is **not elevated**, like Chrome and the shell running the app |
| Window structure | Both are Chromium windows: top-level `Chrome_WidgetWin_1`, a `Chrome_RenderWidgetHostHWND` child holding the page. Claude's top-level is also `WS_EX_LAYERED` + `WS_EX_NOREDIRECTIONBITMAP` (no effect on input) |
| Tracking of the previous window | `ForegroundTracker` remembers the top-level window; Claude has a single visible one, so the target is right |

**Leading hypothesis — typed too early.** `SetForegroundWindow` only queues the activation: the target
thread handles `WM_ACTIVATE` / `WM_SETFOCUS` later, and in a Chromium app the focus inside the page is
restored asynchronously again (browser process → renderer). A web app may also put the focus back on its
own field from a `focus` handler. Characters arriving before that land on no editable element and are
dropped. Chrome restores its page's focus fast enough; the Claude app — a heavier page, possibly
re-focusing its prompt from script — does not.

To be **confirmed by a live test** before the fix is frozen (see *Open Questions*).

---

## Proposed Fix

Type only once the target is **ready**, without blocking the UI thread:

- After `Activate`, poll (WinForms timer, ~10 ms) until the target is the foreground window **and** its
  GUI thread has a focused window (`GetGUIThreadInfo(…).hwndFocus`), then wait a short **settle delay**
  before `SendInput`; give up waiting after a **timeout** and type anyway (never worse than today).
- The same wait for `InsertKeepingOpen`: the hand-back (`KeepOpenHandBackDelay`) is counted from the typing,
  not from the activation, and the hook swallows keys for the whole wait.
- Delay and timeout values: set from the live test (see *Open Questions*).

---

## Test Impact

The repository has **no test project**, and the change is Win32 timing (`SetForegroundWindow`,
`GetGUIThreadInfo`, `SendInput`): nothing unit-testable. Checked by hand / by a live script in the Claude
app, Chrome and the Notepad.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none (no test project; interop timing only) | — | — |

---

## Open Questions

- [ ] Confirm the cause with a live test: a script activates the Claude window from another window, then
      types a marker immediately, then another one after waiting — which one arrives?
- [ ] Wait strategy: readiness polling + settle delay (proposed), or a fixed delay only?
- [ ] Normal (hiding) insertion: swallow the user's keys during the wait, like the keep-open one?

---

## Design Iterations

### Iteration 1 — 2026-10-09

Scope from the user: nothing is inserted in the Claude desktop app's prompt box, no other app tested, no
clipboard fallback, a simple subject. Scout pass: UIPI ruled out, both targets are plain Chromium windows.
Leading hypothesis: the emoji is typed before the target has its focus back; proposed fix: wait for the
target to be ready, then type.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project — see *Test Impact* |
| README | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What exactly happens when the insertion fails in Claude Code? | Nothing is inserted | 2026-10-09 |
| 2 | Which other apps fail? | None other tested | 2026-10-09 |
| 3 | Is a clipboard fallback acceptable? | No, never | 2026-10-09 |
| 4 | Is the subject straightforward or tricky? | Straightforward | 2026-10-09 |

---

*Last updated: 2026-10-09*
