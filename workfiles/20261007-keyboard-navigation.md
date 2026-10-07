# Keyboard Navigation

> Working document — move a selection through the emoji grid with the keyboard (arrows, Home / End,
> Page Up / Page Down, Tab / Shift+Tab for the categories), insert the selected emoji with Enter,
> hide the window with Esc.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The grid gets a **selection**: one emoji, highlighted. The keyboard moves it; **Enter** inserts it
into the previous window exactly like a click; **Esc** hides the window to the tray, inserting
nothing. The mouse and the keyboard share the **same selection**: hovering an emoji selects it, so
there is a single highlight on screen.

| In scope | Out of scope |
|---|---|
| Arrows, Home / End, Page Up / Page Down in the grid | The search box and its own keys ([20261007-search-box.md](20261007-search-box.md), still in design) |
| Tab / Shift+Tab to the next / previous category | Recents, skin tones, flags ([TODO-FEATURES.md](TODO-FEATURES.md)) |
| Enter inserts the selected emoji, Esc hides the window | Keyboard access to the tab strip itself (focusing a tab) |
| Hover and keyboard share one selection | Persisting the selection between two launches |

Backlog: this covers the whole **Keyboard navigation** row of
[TODO-FEATURES.md](TODO-FEATURES.md) (arrows, Enter, Tab / Shift+Tab) — see Open Questions.

### Starting point

| Fact | Source |
|---|---|
| The grid is one continuous scrolling grid, one section per category, each section starting on a new row; `EmojiGridLayout` alone knows the geometry (`Columns`, `CellBounds`, `HeaderTop`, `SectionAt`, `HitTest`) | `UI/EmojiGridLayout.cs` |
| The grid tracks a `hovered` cell, filled with `SystemColors.ControlLight`, its name shown as a tooltip; a left click raises `EmojiClicked` | `UI/EmojiGrid.cs` |
| `OnScrolled` re-hit-tests the cursor after every scroll: the hovered cell changes under a mouse that did not move | `EmojiGrid.OnScrolled` → `SetHovered(PointToClient(Cursor.Position))` |
| Neither the grid nor the tab strip is selectable (`ControlStyles.Selectable = false`): the form has no focusable control, key handling belongs to `MainForm` | `UI/EmojiGrid.cs`, `UI/CategoryTabStrip.cs` |
| Arrows, Tab and Enter are dialog keys: WinForms consumes them before `KeyDown` unless they are handled in `ProcessCmdKey` / `ProcessDialogKey` | WinForms behaviour |
| `MainForm.InsertEmoji(Emoji)` does the whole click path (previous window back to the front, hide, `SendInput`, tray icon updated) | `UI/MainForm.cs` |
| A tab click scrolls its section's header to the top (`EmojiGrid.ScrollToCategory`) | `UI/MainForm.cs` |
| No test project — every earlier workfile checked by hand | [20261007-category-tabs.md](20261007-category-tabs.md) Q&A #13 |

---

## Selection

- `EmojiGrid` holds **one selection** `(Section, Index)` — it replaces `hovered`. It is drawn with
  the highlight the hover has today; its name is the tooltip (see Open Questions for the keyboard
  case and the highlight's look).
- **Window shown** → the selection is the **first emoji of the grid**, so Enter inserts at once.
- **Mouse**: moving over an emoji selects it. Leaving the grid keeps the selection (the keyboard
  must still have one to act on — see Open Questions).
- **Kept in view**: every keyboard move scrolls the grid by the least amount that shows the
  selected cell entirely. The active tab follows the scroll, as today.
- **A scroll does not move the selection**: the re-hit-test of `OnScrolled` stops changing it —
  otherwise a keyboard scroll would hand the selection to the emoji under a cursor that never
  moved. Only a real mouse move does.

---

## Keys

Handled by `MainForm` (no control takes the focus), forwarded to `EmojiGrid`. The target cell is
computed from `EmojiGridLayout`'s geometry, in **grid order**: sections one after the other, each
read row by row.

| Key | Does |
|---|---|
| ← / → | Previous / next emoji in grid order (row ends and section ends: see Open Questions) |
| ↑ / ↓ | The emoji one row above / below, same column (short rows and section boundaries: see Open Questions) |
| Home / End | First / last emoji (of the grid or of the category: see Open Questions) |
| Page Up / Page Down | As many rows up / down as the viewport holds, same column |
| Tab / Shift+Tab | First emoji of the next / previous category, its header scrolled to the top — like a tab click |
| Enter | Inserts the selected emoji: `MainForm.InsertEmoji`, the click path |
| Esc | Hides the window to the tray, nothing inserted — the same as ✕ |

- Holding a key repeats it (Windows auto-repeat), nothing added.
- Modifiers other than Shift on Tab are not handled: Ctrl, Alt, Win combinations keep their
  default behaviour (Alt+F4 still hides to the tray).

---

## Documentation

- `RULES.md` gains a **Keyboard** table next to *Window and Tray Icon*, and the click row's
  insertion line mentions Enter.
- `README.md` / `README.fr.md`: the keys in the usage section, both languages in the same commit.
- `GLOSSARY.md` / `GLOSSARY.fr.md`: **Selection** (*sélection*) — the highlighted emoji the keyboard
  and the mouse move, the one Enter inserts.

---

## Test Impact

Pending Open Question 10. If no test project is created, nothing is pinned in a test file and
everything is checked by hand:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| Target cell of each key: row ends, short last rows, section boundaries, grid edges, Page Up / Down | `tests/EmojiSelector.Tests/UI/EmojiGridLayoutTests.cs` (if a test project is created) | Create |

---

## Open Questions

- [ ] 1. **← / → at a row end**: → on the last emoji of a row goes to the first of the next row,
  and across sections (last of a category → first of the next one), like Win+;? Or stops at the
  row end?
- [ ] 2. **↑ / ↓ across a short row**: ↓ from a column the row below does not have (the last row of
  a section is often short) → the last emoji of that row? Or skip to the next row that has the
  column? And ↓ from the last row of a section goes into the next section's first row, same column?
- [ ] 3. **Grid edges**: ← on the very first emoji, → on the very last, ↑ on the first row, ↓ on
  the last — stay put, or wrap to the other end?
- [ ] 4. **Home / End**: first / last emoji of the **whole grid**, or of the **current category**
  (the selection's)? (Ctrl+Home / Ctrl+End could carry the other one.)
- [ ] 5. **Tab past the last category** (Shift+Tab before the first): wrap around, or stay?
- [ ] 6. **Selection highlight**: keep today's hover fill (`ControlLight`), or make it stronger
  for the keyboard — an accent frame, like Win+;?
- [ ] 7. **Name of a keyboard-selected emoji**: shown as a tooltip at the cell (as the hover does),
  or nothing until the mouse hovers?
- [ ] 8. **Window shown again**: the selection back on the first emoji means scrolling back to the
  top every time — or keep the scroll position and select the first **visible** emoji?
- [ ] 9. **Tab click with the mouse**: also moves the selection to that category's first emoji, like
  Tab does? And the mouse leaving the grid: the selection stays (proposed) or is cleared?
- [ ] 10. **Unit tests**: by hand like the earlier workfiles, or create an xUnit test project for
  the key → target cell logic (a pure function of `EmojiGridLayout`)?
- [ ] 11. **Search box overlap**: [20261007-search-box.md](20261007-search-box.md) (in design) asks
  its own Enter / Esc question. This workfile defines Enter / Esc for the grid alone, and the search
  box adapts when it lands — agreed?
- [ ] 12. **Backlog**: mark the *Keyboard navigation* row of [TODO-FEATURES.md](TODO-FEATURES.md)
  with this workfile (the whole row is covered)?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-07

Initial design from the scoping batch (Q&A 1–4): arrows and Enter, plus Tab / Shift+Tab,
Home / End, Page Up / Page Down and Esc; the first emoji selected when the window appears; hover and
keyboard share one selection; a simple subject, explored in a single pass.

The code read showed that `EmojiGrid.OnScrolled` re-hit-tests the cursor after every scroll, which
would let a still mouse steal the selection after a keyboard scroll: the design moves the selection
on real mouse moves only. Keys go through `MainForm` since no control is focusable.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |
| RULES.md, glossary | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which keys, besides the arrows and Enter, are in this workfile? | Tab / Shift+Tab, Home / End, Page Up / Page Down, Esc | 2026-10-07 |
| 2 | When the window appears, which emoji is selected? | The first of the grid | 2026-10-07 |
| 3 | How do the mouse and the keyboard selection coexist? | Hover moves the selection (one shared selection) | 2026-10-07 |
| 4 | Is the subject straightforward or tricky / long to explore? | Straightforward | 2026-10-07 |
| 5 | ← / → at a row end: continue on the next row and across sections, or stop? (Open Question 1) | | |
| 6 | ↑ / ↓ across a short row and a section boundary (Open Question 2) | | |
| 7 | Grid edges: stay put or wrap? (Open Question 3) | | |
| 8 | Home / End: whole grid or current category? (Open Question 4) | | |
| 9 | Tab past the last category: wrap or stay? (Open Question 5) | | |
| 10 | Selection highlight: hover fill or accent frame? (Open Question 6) | | |
| 11 | Name of a keyboard-selected emoji: tooltip or nothing? (Open Question 7) | | |
| 12 | Window shown again: back to the top, or first visible emoji? (Open Question 8) | | |
| 13 | Tab click moves the selection? Mouse leaving the grid keeps it? (Open Question 9) | | |
| 14 | Unit tests: by hand or a test project? (Open Question 10) | | |
| 15 | Search box overlap: this workfile owns Enter / Esc for the grid, the search box adapts? (Open Question 11) | | |
| 16 | Backlog: mark the *Keyboard navigation* row with this workfile? (Open Question 12) | | |

---

*Last updated: 2026-10-07*
