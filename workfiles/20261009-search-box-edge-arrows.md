# Search Box Edge Arrows

> Working document — ← / → at the edge of the search box's text move into the grid.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

With text in the search box, ← / → move the text caret, and only ↓ hands the keyboard to the grid
(RULES.md § Keyboard). Once the caret sits at the **end** of the text, → has nowhere left to go in
the box: it should act as → in the grid, from the selection — the grid gets the keyboard, the
selection moves to the next emoji. Likewise ← with the caret at the **start** of the text.

Inside the text, ← / → keep moving the caret: editing is never disturbed.

Components: `UI/MainForm.cs` (`ProcessCmdKey`, the key routing) only. `EmojiGrid.MoveSelection` and
`EmojiGridLayout` are unchanged — the target cells are already computed there.

---

## Key Routing

In `MainForm.ProcessCmdKey`, search box focused, **with text** (`TextLength > 0`):

| Key | Caret | Does |
|---|---|---|
| → (no modifier) | At the end of the text, **no text selected** (`SelectionLength == 0`, `SelectionStart == TextLength`) | As → in the grid, from the selection: the selection moves to the next emoji, the grid gets the keyboard |
| ← (no modifier) | At the start of the text, no text selected (`SelectionLength == 0`, `SelectionStart == 0`) | As ← in the grid, from the selection: the previous emoji, the grid gets the keyboard |
| ← / → | Anywhere else, or text selected | Windows' own behaviour: the caret moves; a text selection collapses to its end / start — the **next** press at the edge leaves the box |
| Shift+← / →, Ctrl+← / → | Any | Windows' own behaviour, never leaves the box |
| ← / → at the edge | **No result** (`grid.SelectedEmoji is null`) | Nothing: the keyboard stays in the box, like ↓ |

- **From the selection, like the empty box**: the grid gets the keyboard **even when the selection
  cannot move** — → on the last result, ← on the first one. The same rule as the empty box's keys
  (RULES.md § Keyboard).
- The selection is the one in place — the first result after typing, or the one the mouse moved
  to — not reset to the first result, unlike ↓.
- The empty box's branch already answers ← / → (its caret is at both edges at once): unchanged.
- Coming back is unchanged: ↑ on the grid's first row, or typing a character, returns to the box —
  `FocusSearchBox` puts the caret at the end of the text, so a → right after leaves again.

---

## Documentation Impact

| File | Change |
|---|---|
| `RULES.md` § Keyboard | The *Search box, with text* row ← / → splits: the caret inside the text; at the edge → as in the grid. Wording on "only ↓ leaves it" updated |
| `README.md` / `README.fr.md` § Keyboard | One clause: **→** at the end of the text (**←** at its start) moves into the grid too |
| `MainForm.ProcessCmdKey` comments | "With text in the box, only ↓ leaves it" updated |

---

## Test Impact

None: the app has **no test project**, and the change is the routing of keys between two WinForms
controls (`MainForm.ProcessCmdKey`), not a computation — the cells a key reaches are already computed
by `EmojiGridLayout`, unchanged. The keys are checked on the built app, by posting `WM_KEYDOWN` to the
box (RULES.md § Keyboard), the caret placed with `EM_SETSEL`.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none, see above | — | — |

---

## Open Questions

- [x] ~~What does → at the end of the text do: hand over the keyboard only, or move as in the grid?~~
  → As in the grid, from the selection (the next emoji); ← at the start likewise. No result → nothing,
  the keyboard stays in the box.
- [x] ~~Text selected in the box (Ctrl+A, Shift+arrows): leave at once when it touches the edge?~~
  → No: Windows' behaviour first (the selection collapses), the next press at the edge leaves.
- [x] ~~Shift / Ctrl + ← / → at the edge?~~ → Stay in the box: only the plain arrows leave it.

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-09

Initial design from the request and the scoping answers (Q&A 1–3): plain → with the caret at the end
of the text, ← at its start, no text selected, act as in the grid from the selection and hand the
keyboard over — even when the selection cannot move; no result → stays in the box. Shift / Ctrl
variants and a selected text keep Windows' behaviour. One branch added to `MainForm.ProcessCmdKey`;
the grid and the layout untouched. No test project: checked on the built app.

### Iteration 2 — 2026-10-09 — ✅ Implemented

Go given: code, unit tests and documentation, in a worktree (`.claude/worktrees/search-box-edge-arrows`,
branch `feature/search-box-edge-arrows`).

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project — see *Test Impact* |
| README | | | |
| RULES.md | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Caret at the end of the text + →: hand over the keyboard only, or move as in the grid? | As in the grid, from the selection; ← at the start likewise; no result → stays in the box | 2026-10-09 |
| 2 | Text selected in the box: leave at once at the edge, or Windows' behaviour first? | Windows' behaviour first, the next press leaves | 2026-10-09 |
| 3 | Shift / Ctrl + ← / → at the edge? | Stay in the box | 2026-10-09 |
| 4 | Exploration depth? | Straightforward — one scout pass | 2026-10-09 |

---

*Last updated: 2026-10-09*
