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

## Fix

Type only once the target is **ready**, without blocking the UI thread:

- After `Activate`, a WinForms timer (`MainForm.typingTimer`, `TypingPollInterval`) polls until the target is
  the foreground window **and** its GUI thread has a focused window (`EmojiInserter.IsReady`,
  `GetGUIThreadInfo(…).hwndFocus`), then waits a **settle delay** (`TypingSettleDelay`) before `SendInput`;
  after a **timeout** (`TypingTimeout`, from the activation) it types anyway (never worse than before).
  `MainForm.TypeWhenReady` starts the wait, `OnTypingTick` ends it; the pending insertion is a
  `PendingTyping` record.
- A hiding insertion whose window was **shown again** during the wait (the tray icon) types nothing: the
  emoji would land in the app's own window.
- **One insertion at a time**: one started while the typing or a keep-open hand-back is pending is ignored
  (the guard moved from `InsertKeepingOpen` to `InsertEmoji`).
- **Normal insertion too**: the hook swallows every physical key-down but the modifiers from the activation
  to the typing (`ShortcutHook.SwallowKeys` / `StopSwallowing`, as the keep-open insertion does), so a key
  typed quickly never lands before the emoji. Those keys are lost. The hook's safety limit
  (`MaxSwallowMilliseconds`) goes from 1 s to **1.5 s**, to cover the timeout, the settle delay and the
  keep-open hand-back.
- The same wait for `InsertKeepingOpen`: the hand-back (`KeepOpenHandBackDelay`) is counted from the typing,
  not from the activation, and the hook swallows keys for the whole wait.
- **Values** (no measurement — tuned after the user's test of the fix): polling every 10 ms, a **settle
  delay of 100 ms** once the target is ready, a **timeout of 1 s** from the activation.
- **Docs**: RULES.md § Categories and Insertion, § Keep-Open Insertion and § Shortcut. The READMEs are
  unchanged: no visible behaviour changes besides the fix.
- **Confirmation by the fix itself**: the user tests the built app in the Claude app's prompt box, Chrome and
  the Notepad. Inserted in Claude → the hypothesis holds; still nothing → back to design (the wait stays,
  harmless).

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

- [x] ~~Confirm the cause with a live test, run by the user: a script activates the Claude window from its own
      window, hides it, types `A🙂` at once, then `B🙂` 300 ms later, and logs when Claude is in front and has a
      focused window — which markers arrive?~~ → The script did not run on the user's side; the fix is
      implemented directly and tested in the app
- [x] ~~Wait strategy: readiness polling + settle delay (proposed), or a fixed delay only?~~ → Readiness polling
      + settle delay, typed anyway after a timeout
- [x] ~~Normal (hiding) insertion: swallow the user's keys during the wait, like the keep-open one?~~ → Yes: the
      hook swallows every physical key-down but the modifiers from the activation to the typing

---

## Design Iterations

### Iteration 1 — 2026-10-09

Scope from the user: nothing is inserted in the Claude desktop app's prompt box, no other app tested, no
clipboard fallback, a simple subject. Scout pass: UIPI ruled out, both targets are plain Chromium windows.
Leading hypothesis: the emoji is typed before the target has its focus back; proposed fix: wait for the
target to be ready, then type.

### Iteration 2 — 2026-10-09

The user runs the live test (`insertion-timing-test.ps1`, from the agent's scratchpad). Wait strategy:
readiness polling + settle delay. The normal insertion swallows the keys during the wait, like the keep-open one.

### Iteration 3 — 2026-10-09

The test script did not run on the user's side: the user asks to implement the fix directly and test it in
the app. Values chosen without measurement: 10 ms polling, 100 ms settle delay, 1 s timeout.

### Iteration 4 — 2026-10-09 — ✅ Implemented

Go given: code and documentation (no test project), in a worktree on `feature/insertion-fails-in-some-editors`.

### Iteration 5 — 2026-10-09 — 🧭 Implementation choices

- **Hook limit raised**: `ShortcutHook.MaxSwallowMilliseconds` 1 s → 1.5 s. The 1 s timeout plus the settle
  delay and the hand-back could outlast the old limit, letting keys through before the emoji.
- **Guard moved**: the "one insertion at a time" check now sits in `InsertEmoji`, covering both paths,
  instead of `InsertKeepingOpen` alone.
- **Shown again during the wait**: a hiding insertion types nothing when its window is visible again by the
  time the emoji is due (tray icon click) — not in the design, added so the emoji never lands in the
  search box.
- **No README change**: the fix changes no documented behaviour.
- No rule broken.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 4, 5 | 2026-10-09 | `EmojiInserter.IsReady`, hook swallowing for every insertion, `MainForm.TypeWhenReady` |
| Unit tests | 4 | 2026-10-09 | Not applicable: no test project — see *Test Impact* |
| README | 4, 5 | 2026-10-09 | RULES.md updated; READMEs unchanged (no visible behaviour change) |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What exactly happens when the insertion fails in Claude Code? | Nothing is inserted | 2026-10-09 |
| 2 | Which other apps fail? | None other tested | 2026-10-09 |
| 3 | Is a clipboard fallback acceptable? | No, never | 2026-10-09 |
| 4 | Is the subject straightforward or tricky? | Straightforward | 2026-10-09 |
| 5 | May the agent run a live test typing markers into the Claude app's prompt box? | No — the user runs the test script | 2026-10-09 |
| 6 | Wait strategy: readiness polling + settle delay, or a fixed delay? | Readiness polling + settle delay | 2026-10-09 |
| 7 | Normal insertion: swallow the user's keys during the wait? | Yes | 2026-10-09 |
| 8 | Results of the live test script? | It did not run — implement directly and test with the fix | 2026-10-09 |

---

*Last updated: 2026-10-09*
