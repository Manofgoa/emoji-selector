# Navigation Keys in the Empty Search Box

> Working document — the grid's navigation keys work from the search box while it is empty.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the search box keeps most navigation keys for itself: ← / → and Home / End move the text caret,
Page Up / Page Down and Tab / Shift+Tab are ignored, and only ↓ hands the keyboard to the grid — the
selection put back on the grid's first emoji, without moving down (RULES.md § Keyboard).

While the box is **empty**, the caret has nothing to move through: those keys are wasted. The request:
in an empty box, the navigation keys act **as if the grid already had the keyboard**, on the current
selection.

Components: `UI/MainForm.cs` (`ProcessCmdKey`, the search box branch), `UI/EmojiGrid.cs`
(`MoveSelection`, reused as it is). A box holding text keeps today's behaviour.

---

## Behaviour

### Agreed

- **When**: the search box has the keyboard and is **empty** (`TextLength == 0`) — a box holding only
  spaces is **not** empty: ← / → still have a caret to move there.
- **The keyboard moves to the grid**, then the key applies there, exactly as `EmojiGrid.MoveSelection`
  applies it when the grid has the keyboard. From then on, the grid's rules hold: a character or
  Backspace sends the keyboard back to the box, ↑ on the first row too.
- **Even when the key moves nothing** — ← on the grid's first emoji, Home on the first emoji of its
  category: the selection stays, the keyboard is in the grid (a letter brings it back to the box).
- **From the current selection** — the first emoji after a show, but the one the mouse moved it to if
  it moved. Not forced back to the first emoji.
- **↓ changes**: in the empty box it goes **one row down** from the selection, like in the grid —
  no longer "the grid's first emoji". In a box holding text, ↓ keeps today's behaviour (the first
  result, the keyboard staying in the box when there is none).

### The keys

| Key, empty box | Does (from the selection) |
|---|---|
| ← / → | Previous / next emoji, across rows and categories |
| ↓ | One row down, same column |
| ↑ | One row up, same column; on the grid's first row → nothing, the keyboard stays in the box |
| Home / End | First / last emoji of the selection's category |
| Ctrl+Home / Ctrl+End | First / last emoji of the grid |
| Page Up / Page Down | As many rows as the viewport holds |
| Tab / Shift+Tab | First emoji of the next / previous category, its header at the top; wraps around |

- ↑ follows from the grid's own rule (↑ on the first row goes back to the box): the box already has
  the keyboard, so nothing happens. From a lower selection (the mouse moved it), ↑ moves up one row
  and the grid gets the keyboard.
- Not navigation keys, unchanged: Enter (inserts the selection, as today), Esc (hides the window when
  the box is empty, as today), Shift+arrows, Ctrl+← / → and the other text-editing keys.
- **Menu key / Shift+F10** keep the box's own menu (Cut, Copy, Paste…), as today: Paste is useful in
  an empty box.

### Box with text

Unchanged: ← / →, Home / End move the caret; Page Up / Page Down, Tab / Shift+Tab are ignored; ↓
hands the keyboard to the grid on the first result.

### Implementation sketch

In `ProcessCmdKey`'s search box branch, before today's `switch`: when the box is empty and the key is
one of the table's, focus the grid and call `this.grid.MoveSelection(keyData)`. When it returns false
(↑ on the first row), the box gets the keyboard back. The `Keys.Down` case stays for a box with text.

---

## Documentation Impact

- `RULES.md` § Keyboard: the search box rows of the table, and the "Where the selection goes" list.
- `README.md` / `README.fr.md` § Keyboard: the sentence on ↓ in the search box, in both languages.

---

## Test Impact

None: the app has **no test project**, and the change is the routing of keys between two WinForms
controls (`MainForm.ProcessCmdKey`), not a computation — the cells a key reaches are already computed
by `EmojiGridLayout`, unchanged. The keys are checked on the built app, by posting `WM_KEYDOWN` to the
box (RULES.md § Keyboard).

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none, see above | — | — |

---

## Open Questions

- [x] ~~A box holding **only spaces** (blank, not empty): does it count as empty?~~ → No: only a box
  with no character at all.
- [x] ~~A key that moves nothing — ← on the grid's first emoji, Home on the first emoji of its
  category: does the keyboard still go to the grid?~~ → Yes, as if the grid had it.
- [x] ~~**Menu key / Shift+F10** in the empty box: the box's own menu or the selection's?~~ → The
  box's menu, unchanged.

---

## Design Iterations

### Iteration 1 — 2026-10-08

Initial design from the request and the scoping answers: in an empty search box, the navigation keys
move the keyboard to the grid and apply from the current selection; ↓ goes one row down there instead
of resetting to the first emoji. A box with text is unchanged. No test project: the Test Impact table
is empty on purpose.

### Iteration 2 — 2026-10-08

The three open questions answered: a box of spaces is not empty; a key that moves nothing still sends
the keyboard to the grid; the Menu key / Shift+F10 keep the box's menu. No question left.

### Iteration 3 — 2026-10-09 — ✅ Implemented

Go given: code, tests and documentation, in a worktree (`.claude/worktrees/empty-search-box-keys`,
branch `feature/empty-search-box-keys`). The scope is the design above, frozen.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project — nothing to write (see *Test Impact*) |
| README | | | |
| RULES.md | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | When a navigation key is pressed in the empty box, where does the keyboard go? | To the grid, the key applied there | 2026-10-08 |
| 2 | From which emoji does the key apply? | The current selection (may have been moved by the mouse) | 2026-10-08 |
| 3 | ↓ in the empty box: unchanged (first emoji) or one row down? | One row down, like in the grid | 2026-10-08 |
| 4 | Is the subject straightforward or tricky? | Straightforward — a single exploration pass | 2026-10-08 |
| 5 | A box holding only spaces counts as empty? | No — empty means no character | 2026-10-08 |
| 6 | A key that moves nothing still sends the keyboard to the grid? | Yes | 2026-10-08 |
| 7 | Menu key / Shift+F10 in the empty box: the box's menu or the selection's? | The box's menu, unchanged | 2026-10-08 |

---

*Last updated: 2026-10-09*
