# Keep-Open Insertion (Ctrl+Enter, Ctrl+Click)

> Working document — Ctrl+Enter and Ctrl+click insert the emoji while the window stays open, the
> keyboard and the selection where they were, so several emojis can be inserted in a row.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today an emoji clicked, or inserted with Enter, goes into the **previous window**, then the window
hides to the tray (RULES.md § Window and Tray Icon, § Categories and Insertion). Inserting the same
emoji three times, or 🎉🎂🥳, means reopening the window between each one.

**Keep-open insertion**: **Ctrl+Enter** and **Ctrl+click** insert the emoji into the previous window
exactly as Enter and a click do — its use counted — but the window **stays open and takes the
foreground back**: the keyboard is where it was (search box or grid), the selection on the same
emoji, the search text kept. The next Ctrl+Enter inserts again, into the same previous window.

Components:

| File | Change |
|---|---|
| `UI/MainForm.cs` | `ProcessCmdKey` (Ctrl+Enter), `InsertEmoji` (a keep-open variant), `OnEmojiUsed` unchanged |
| `UI/EmojiGrid.cs` | `OnMouseClick` (Ctrl+click), `ReplaceCategory` (keeps the selection on its emoji) |
| `Input/EmojiInserter.cs` | Typing with Ctrl held (see *Modifiers* below), its injected keys marked |
| `Input/ShortcutHook.cs` | Swallows the keys pressed during the hand-back (see *Keys During the Hand-Back* below) |

---

## Triggers

| Input | Where | Does |
|---|---|---|
| **Ctrl+Enter** (`Keys.Control \| Keys.Enter`, no other modifier) | Search box or grid focused | Keep-open insertion of the **selection** — nothing when there is none, or when it is in the group in reorder mode (like Enter) |
| **Ctrl+click** (left button, `Control.ModifierKeys == Keys.Control` exactly) | An emoji of the grid | Keep-open insertion of that emoji — never one of the group in reorder mode (like a click) |
| Enter, click, Shift / Alt combinations | — | Unchanged: Enter and a plain click insert then hide; the others do what they do today |

- `EmojiGrid` tells which kind of click it was: `EmojiClicked` carries the keep-open flag (a small
  event-args record, or a second event — decided at implementation).
- **No previous window** (`ForegroundTracker.PreviousWindow` is zero): nothing typed, the window
  stays — the use counted, as Enter counts it today with no target.

---

## Insertion Sequence

`MainForm.InsertEmoji(emoji, keepOpen: true)`:

1. **Topmost** (Q&A #6): the window is made `HWND_TOPMOST` (`SetWindowPos`, no move, no size, no
   activation) for the duration, so the previous window brought to the front never covers it, not
   even for an instant. The hook starts swallowing (see *Keys During the Hand-Back*).
2. `EmojiInserter.Activate(previous)` — as today, while the app is still in front (only the
   foreground app may hand the foreground over).
3. **No `Hide()`**.
4. `EmojiInserter.Type(emoji.Text)` — with the modifiers handled (see *Modifiers*).
5. **Hand-back**: once the previous window has received the injected keys, the window takes the
   foreground back with the existing `MainForm.TakeForeground` (being the last app to send input
   lets it; `AttachThreadInput` otherwise). `SendInput` is asynchronous: taking the foreground back
   right after it could route the injected keys to this window. A short delay, one constant
   (`KeepOpenHandBackDelay`, ~50 ms, a WinForms timer — never a `Thread.Sleep` on the UI thread),
   separates the two.
6. Topmost removed (`HWND_NOTOPMOST`, the window staying above the previous one), the hook stops
   swallowing.
7. `OnEmojiUsed(emoji.Text)` — the counter, the frequent section rebuilt **at once** (Q&A #2), see
   *Selection and View*.

- Activating the window back restores its `ActiveControl`: the keyboard is in the search box or
  the grid, as before (Q&A #3). No `OnVisibleChanged`: the box is not cleared, the grid not
  scrolled to the top.
- The previous window stays the target: `ForegroundTracker` skips the app's own windows, so taking
  the foreground back does not replace it.
- A keep-open insertion started while a hand-back is pending waits for it (or is ignored — decided
  at implementation, reported).

### Modifiers

Ctrl is **physically held** while the characters are typed: the previous window would see
`Ctrl` down with each `VK_PACKET` — some apps read it as a shortcut. The injection (Q&A #7)
**releases the held Ctrl keys** — left and right, each one held (`GetAsyncKeyState`) getting an
injected key-up — before the characters, and **presses them again** (an injected key-down each)
after, in the same `SendInput` call: the target sees plain characters, and the keyboard state
matches the user's fingers once done, ready for the next Ctrl+Enter. Shift and Alt are never held
here (the triggers exclude them). A plain Enter or click types as today: nothing is held.

### Keys During the Hand-Back

Between the activation of the previous window (step 2) and the hand-back (step 5), the keyboard
goes to the previous window: a key pressed in that gap — a second Ctrl+Enter in quick succession,
Enter's auto-repeat while held — would land **there** (Ctrl+Enter sends a message in many chat
apps). The shortcut's low-level hook (`Input/ShortcutHook.cs`, already on its own thread)
**swallows them** (Q&A #8):

- While a keep-open insertion runs (steps 1 to 6), the hook swallows every **physical** key-down
  and key-up **except the modifiers** (Ctrl, Shift, Alt, Windows keys — their state must keep
  following the user's fingers). Those keys are **lost**: never typed into the previous window,
  nor into this one.
- The insertion's own keys — the characters and the Ctrl up / down of *Modifiers* — are injected
  with the hook's marker in `dwExtraInfo` (`ShortcutHook.InjectedMarker`, shared), so the hook
  lets them through, like its dummy key.
- The flag is set and cleared from the UI thread, read by the hook's thread (a `volatile` field or
  `Interlocked`): the hook still does nothing but read it and answer.
- A safety: the flag is cleared in a `finally`, and expires on its own after a bound (one second)
  should the hand-back never run — the keyboard is never left swallowed.

---

## Selection and View

The frequent section is rebuilt at once after each use (Q&A #2): `EmojiGrid.ReplaceCategory`
today puts the selection back on the first emoji in view — the keep-open insertion needs it to
**stay on the emoji**.

- **`ReplaceCategory` keeps the selection on its emoji**:

  | Selection before | After |
  |---|---|
  | In the replaced section (the frequent one) | The same emoji, wherever it moved in the new section (it was just used: it can only go up) |
  | In another section | The same cell — the same emoji |
  | Gone (removed, *Clear frequently used*), or none | The first emoji in view, as today |

  It applies to every call — *Remove from frequently used* and *Clear* included —, with no visible
  change for them: the emoji they act on is gone. After a plain Enter or click, the window hides,
  and every show resets the selection anyway.
- **The view** (Q&A #5): while the frequent section grows (its first 3 rows), the sections below it
  move down — a Ctrl+click repeated at the same place would hit another emoji. When the selection is
  **outside** the replaced section, the view moves with the change of height (`SetOffset`, as
  `ReplaceCategories` does for the sections below it), so the selected emoji keeps its place on
  screen — under the mouse for a Ctrl+click —, even when the view was at the very top. In the
  frequent section itself, the view stays: the emoji moves inside it.
- **Search mode**: unchanged — the frequent section is not shown, `ReplaceCategory` only stores it;
  the results and their selection do not move.
- The details panel follows `SelectedEmojiChanged`, as today.

---

## Test Impact

None: the app has **no test project**, and the change is window activation, input injection and
the grid's selection across a section replacement — WinForms and Win32 behaviour, checked on the
built app: Ctrl+Enter posted as `WM_KEYDOWN` to the box (RULES.md § Keyboard), a Notepad window as
the previous window, its text read back after several insertions; the window still visible and in
front (`GetForegroundWindow`), the box's text kept.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none, see above | — | — |

---

## Open Questions

- [x] ~~**View on a repeated Ctrl+click**: while the frequent section grows, keep the selected emoji
      at its place on screen when it is outside that section, or let the view stay where it is?~~
      → The selected emoji keeps its place on screen (Q&A #5)
- [x] ~~**Topmost during the insertion**: make the window topmost from the activation of the
      previous window to the hand-back, or accept a flicker?~~ → Topmost for the insertion (Q&A #6)
- [x] ~~**Modifiers**: release the held Ctrl keys around the typed characters and press them again
      after, or type as Enter does today?~~ → Released then pressed again (Q&A #7)
- [x] ~~**Keys during the hand-back**: the shortcut hook swallows the keys pressed in the ~50 ms gap,
      or they go to the previous window?~~ → Swallowed by the hook, modifiers excepted (Q&A #8)

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-09

Initial design, from the user's request and the scoping answers (Q&A #1–4): Ctrl+Enter and
Ctrl+click insert without hiding, the window takes the foreground back, the keyboard stays where it
was, the frequent section is rebuilt at once with the selection kept on its emoji. No row of
`TODO-FEATURES.md` matches the request. Four technical points left open: the view on a repeated
Ctrl+click, topmost during the insertion, the held Ctrl key, the keys pressed during the hand-back.

### Iteration 2 — 2026-10-09

The four open points answered (Q&A #5–8), each as proposed: the selected emoji keeps its place on
screen when the frequent section grows; the window topmost for the insertion; the held Ctrl keys
released around the typed characters then pressed again; the keys pressed during the hand-back
swallowed by the shortcut hook, the modifiers and the insertion's own marked keys let through, the
flag bounded to one second. No open question left.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | — | — | Not applicable: no test project (see *Test Impact*) |
| RULES.md | | | § Window and Tray Icon, § Categories and Insertion, § Keyboard |
| README (English and French) | | | § Keyboard, the click sentence |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Does Ctrl+click do the same (insert, the window kept open)? | Yes, Ctrl+click too | 2026-10-09 |
| 2 | The frequent section may reorder after each use: update it on hide, or at once? | At once — the selection put back on the same emoji | 2026-10-09 |
| 3 | After a Ctrl+Enter, where is the keyboard? | Where it was (search box or grid), the selection unmoved, the search text kept | 2026-10-09 |
| 4 | Is the subject straightforward or tricky? | Straightforward — a single exploration pass | 2026-10-09 |
| 5 | View on a repeated Ctrl+click while the frequent section grows | The selected emoji keeps its place on screen (under the mouse) | 2026-10-09 |
| 6 | Topmost during the insertion | Yes, topmost for the insertion | 2026-10-09 |
| 7 | Modifiers: release the held Ctrl around the typed characters | Released, then pressed again | 2026-10-09 |
| 8 | Keys pressed during the hand-back gap | Swallowed by the hook (lost), modifiers excepted | 2026-10-09 |

---

*Last updated: 2026-10-09*
