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
| | Showing the name of a keyboard-selected emoji (the hover tooltip stays for the mouse) |

Backlog: this covers the whole **Keyboard navigation** row of
[TODO-FEATURES.md](TODO-FEATURES.md) (arrows, Enter, Tab / Shift+Tab), marked with this workfile.

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
| *(2026-10-08)* The **search box is delivered** (merge `e288bb9`): a `TextBox` above the tabs, cleared and focused on every show (`OnVisibleChanged` → `ClearSearch`); `OnSearchBoxKeyDown` handles Enter (inserts `searchResults[0]`) and Esc (clears, or hides) | `UI/MainForm.cs` |
| *(2026-10-08)* Search mode swaps the grid's sections: `EmojiGrid.ShowSearchResults` (one `Search results` section, scrolled to the top, the category offset kept in `categoriesOffset`) / `ShowCategories` (that offset restored) | `UI/EmojiGrid.cs` |
| *(2026-10-08)* The borderless window and the fast emoji display are merged too: the tab strip carries a close cross and a settings button; cells not rendered yet are filled with a placeholder colour | `UI/CategoryTabStrip.cs`, `UI/EmojiGrid.cs` |

---

## Selection

- `EmojiGrid` holds **one selection** `(Section, Index)` — it replaces `hovered`. It is drawn as a
  **frame in the accent colour** (`SystemColors.Highlight`), like Win+; — no grey fill any more.
- **Name**: the tooltip still shows the name of the emoji under the **mouse**; a keyboard-selected
  emoji shows no name (out of scope).
- **Window shown** → the grid is scrolled back to the **top** and the selection is the **first emoji
  of the grid**, so Enter inserts at once (see Open Questions for the search box's show rule).
- **Mouse**: moving over an emoji selects it. Leaving the grid **keeps** the selection — the
  keyboard still has one to act on. A **tab click** selects the first emoji of that category, like
  Tab.
- **Kept in view**: every keyboard move scrolls the grid by the least amount that shows the
  selected cell entirely. The active tab follows the scroll, as today.
- **A scroll does not move the selection**: the re-hit-test of `OnScrolled` stops changing it —
  otherwise a keyboard scroll would hand the selection to the emoji under a cursor that never
  moved. Only a real mouse move does.

---

## Keys

Handled by `MainForm` and forwarded to `EmojiGrid` while the **grid has the keyboard** (see *With
the search box* for the two focus places). The target cell is computed from `EmojiGridLayout`'s
geometry, in **grid order**: sections one after the other, each read row by row — the category
sections, or the single `Search results` section in search mode.

| Key | Does |
|---|---|
| ← / → | Previous / next emoji in grid order: past a row end onto the next row, past a category's last emoji onto the next category's first, like Win+; |
| ↑ / ↓ | The emoji one row above / below, same column. The row there is shorter → its **last** emoji. From a category's last row, ↓ enters the next category's first row (↑ likewise the previous one's last row) |
| Home / End | First / last emoji of the **current category** (the selection's) |
| Ctrl+Home / Ctrl+End | First / last emoji of the **whole grid** |
| Page Up / Page Down | As many rows up / down as the viewport holds, same column, the rows counted across categories as ↑ / ↓ do; stops on the first / last row |
| Tab / Shift+Tab | First emoji of the next / previous category, its header scrolled to the top — like a tab click. **Wraps**: Tab on the last category goes to the first, Shift+Tab on the first to the last |
| Enter | Inserts the selected emoji: `MainForm.InsertEmoji`, the click path |
| Esc | Hides the window to the tray, nothing inserted — the same as ✕. With the search box, its rule comes first: a box holding text is cleared instead |

- **Grid edges**: ← on the very first emoji, → on the very last, ↑ on the first row, ↓ on the last
  row → nothing moves (no wrap). Only Tab / Shift+Tab wrap.
- Holding a key repeats it (Windows auto-repeat), nothing added.
- Modifiers other than Shift on Tab and Ctrl on Home / End are not handled: other Ctrl, Alt, Win
  combinations keep their default behaviour (Alt+F4 still hides to the tray).

### With the search box

The search box ([20261007-search-box.md](20261007-search-box.md)) is delivered: this workfile is
implemented on top of it, and the keyboard has **two places** — the search box (focused on every
show) and the grid.

| Focus | Key | Does |
|---|---|---|
| Search box | ← / →, Home / End, Ctrl+Home / Ctrl+End | The text caret, as in any text box |
| Search box | ↓ | The **grid** takes the keyboard; the selection goes to the **first emoji** (the first result in search mode) |
| Search box | Enter | Inserts the **selection** — the first emoji of the grid when the box is blank, the first result in search mode unless the arrows moved it. Replaces the search box's *nothing when the box is blank* |
| Search box | Esc | Unchanged: clears the box, or hides the window when it is empty |
| Search box | Page Up / Page Down, Tab / Shift+Tab | **Ignored** — only ↓ leaves the box |
| Grid | ↑ on the grid's **first row** | Back to the **search box** — instead of staying put (*Grid edges*) |
| Grid | A character typed | Back to the **search box**, the character typed into it |
| Grid | Every other key of the table above | As described there |

- **Search mode**: on every change of the text, the selection goes to the **first result**; Enter
  inserts the **selection** — the search box's *first result* unless the arrows moved it.
- **Tab / Shift+Tab in search mode** are **ignored**, like clicks on the greyed tabs.
- **Window shown**: back to the top, first emoji selected — this workfile's rule wins over the search
  box's *scroll position before the search*, and its workfile is aligned (its Iteration 5). Emptying
  the box while the window stays open still brings the pre-search position back.
- The selection frame stays drawn while the box has the focus: it shows what Enter inserts.

---

## Documentation

- `RULES.md` gains a **Keyboard** table next to *Window and Tray Icon*, and the click row's
  insertion line mentions Enter; its § Search Box follows the new rules (Enter on the selection,
  ↓ / ↑ / typed character, back to the top on show).
- `README.md` / `README.fr.md`: the keys in the usage section, both languages in the same commit.
- `GLOSSARY.md` / `GLOSSARY.fr.md`: **Selection** (*sélection*) — the highlighted emoji the keyboard
  and the mouse move, the one Enter inserts.

---

## Test Impact

**No unit test** — deliberately: there is no test project and the user chose to check by hand, like
the earlier workfiles (Q&A #14).

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none (checked by hand: row ends and section ends, short last rows, grid edges, Home / End and their Ctrl variants, Page Up / Down, Tab wrap, Enter, Esc, hover vs keyboard scroll, accent frame, first emoji on show) | — | — |

---

## Open Questions

- [x] ~~1. **← / → at a row end**: continue on the next row and across sections, or stop?~~ →
  Continue, across rows and categories, like Win+;
- [x] ~~2. **↑ / ↓ across a short row**: last emoji of that row, or skip it?~~ → The row's last
  emoji; ↓ from a category's last row enters the next category, same column
- [x] ~~3. **Grid edges**: stay put, or wrap?~~ → Stay put
- [x] ~~4. **Home / End**: whole grid or current category?~~ → Current category; Ctrl+Home /
  Ctrl+End for the whole grid
- [x] ~~5. **Tab past the last category**: wrap or stay?~~ → Wrap (Shift+Tab likewise)
- [x] ~~6. **Selection highlight**: hover fill or accent frame?~~ → Accent frame
- [x] ~~7. **Name of a keyboard-selected emoji**: tooltip or nothing?~~ → Out of scope: no name for
  the keyboard; the hover tooltip stays for the mouse
- [x] ~~8. **Window shown again**: back to the top, or first visible emoji?~~ → Back to the top,
  first emoji selected — but see question 16 (the search box restores another position)
- [x] ~~9. **Tab click / mouse leaving the grid**~~ → A tab click selects the category's first
  emoji; leaving the grid keeps the selection
- [x] ~~10. **Unit tests**: by hand or a test project?~~ → By hand, no test project
- [x] ~~11. **Search box overlap**: this workfile owns Enter / Esc, the search box adapts?~~ → No:
  wait for the search box's keys to be settled, then align this workfile on them. They were settled
  on 2026-10-08 (its Q3: Enter inserts the first result; Esc clears the box, or hides the window when
  it is empty) — the alignment raises questions 13–16
- [x] ~~12. **Backlog**: mark the *Keyboard navigation* row?~~ → Yes, marked with this workfile
- [x] ~~13. **Keys while the search box has the focus**: ← / → and Home / End to the grid, the
  caret, or depending on the text?~~ → Two focus places: in the box the caret keys stay the box's,
  ↓ hands the keyboard to the grid on the first emoji; ↑ (from the grid's first row) or a typed
  character goes back to the box
- [x] ~~14. **Selection in search mode**~~ → On the first result after every text change; Enter
  inserts the selection
- [x] ~~15. **Tab / Shift+Tab in search mode**~~ → Ignored
- [x] ~~16. **Window shown**: back to the top, or the search box's pre-search position?~~ → Back to
  the top; the search box workfile is aligned
- [x] ~~17. **Enter in an empty search box, right after a show**: insert the first emoji, or
  nothing?~~ → Inserts the first emoji (the selection)
- [x] ~~18. **Page Up / Page Down and Tab while the box has the focus**~~ → Ignored: only ↓ leaves
  the box
- [x] ~~19. **Implementation order**: after or before the search box?~~ → After — and the search box
  turned out to be delivered already (merge `e288bb9`): the focus model is built in from the start
- [x] ~~20. **Aligning the search box workfile**: this session, its own, or later?~~ → This session:
  [20261007-search-box.md](20261007-search-box.md) Iteration 5, its code changed by this
  workfile's run

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

### Iteration 2 — 2026-10-08

Open Questions 1–12 answered (Q&A 5–16): continuous ← / → across rows and categories; a short row
takes ↓ to its last emoji; no wrap at the grid edges, only Tab / Shift+Tab wrap; Home / End for the
category, Ctrl+Home / Ctrl+End for the grid; an accent frame for the selection; no name shown for a
keyboard selection (out of scope); back to the top on every show; a tab click moves the selection,
the mouse leaving the grid keeps it; checked by hand; the backlog row marked.

The user chose to wait for the search box's keys before settling the overlap. They were settled in
[20261007-search-box.md](20261007-search-box.md) (Iteration 2, 2026-10-08): Enter inserts the first
result, Esc clears the box or hides the window. Aligning on them raises Open Questions 13–16 — the
caret keys in the focused box, the selection in search mode, Tab in search mode, and a conflict on
the scroll position when the window is shown.

### Iteration 3 — 2026-10-08

Open Questions 13–16 answered (Q&A 17–20). The user went beyond the offered choices for the caret
keys: the keyboard gets **two focus places** — in the search box, the caret keys are the box's and
↓ hands the keyboard to the grid on the first emoji; ↑ (read as: from the grid's first row, the
only place where ↑ has nothing else to do) or a typed character brings it back to the box. Search
mode keeps the selection on the first result, Enter inserts the selection, Tab is ignored there; on
show, this workfile's *back to the top* wins over the search box's restored position. New section
*With the search box*; the follow-ups are Open Questions 17–20.

### Iteration 4 — 2026-10-08

Open Questions 17–20 answered (Q&A 21–24): Enter in the blank box inserts the selection (the first
emoji); only ↓ leaves the box, Page Up / Down and Tab are ignored there; implemented after the search
box; this session aligns the search box workfile.

Checking the order showed the search box already **delivered and merged** (`e288bb9`), with the
borderless window and the fast emoji display: *Starting point* refreshed, the keys now hand over from
the box's `OnSearchBoxKeyDown`. [20261007-search-box.md](20261007-search-box.md) gets its
`⚙️ Post-implementation` entry (Iteration 5) for the show rule and Enter; its code is changed by this
workfile's run. No Open Question left.

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
| 5 | ← / → at a row end: continue on the next row and across sections, or stop? (Open Question 1) | Continue | 2026-10-08 |
| 6 | ↑ / ↓ across a short row and a section boundary (Open Question 2) | The short row's last emoji; ↓ enters the next category | 2026-10-08 |
| 7 | Grid edges: stay put or wrap? (Open Question 3) | Stay put | 2026-10-08 |
| 8 | Home / End: whole grid or current category? (Open Question 4) | Category; Ctrl+Home / Ctrl+End for the grid | 2026-10-08 |
| 9 | Tab past the last category: wrap or stay? (Open Question 5) | Wrap | 2026-10-08 |
| 10 | Selection highlight: hover fill or accent frame? (Open Question 6) | Accent frame | 2026-10-08 |
| 11 | Name of a keyboard-selected emoji: tooltip or nothing? (Open Question 7) | Out of scope | 2026-10-08 |
| 12 | Window shown again: back to the top, or first visible emoji? (Open Question 8) | Back to the top | 2026-10-08 |
| 13 | Tab click moves the selection? Mouse leaving the grid keeps it? (Open Question 9) | Tab click moves it; leaving keeps it | 2026-10-08 |
| 14 | Unit tests: by hand or a test project? (Open Question 10) | By hand | 2026-10-08 |
| 15 | Search box overlap: this workfile owns Enter / Esc for the grid, the search box adapts? (Open Question 11) | Wait for the search box, then align on it | 2026-10-08 |
| 16 | Backlog: mark the *Keyboard navigation* row with this workfile? (Open Question 12) | Yes | 2026-10-08 |
| 17 | Keys while the search box has the focus: ← / → and Home / End to the grid, the caret, or depending on the text? (Open Question 13) | In the box: ↓ gives the grid the selection on the first emoji; ↑ or typing characters goes back to the box | 2026-10-08 |
| 18 | Search mode: selection on the first result after every text change, Enter inserts the selection? (Open Question 14) | Yes, first result | 2026-10-08 |
| 19 | Tab / Shift+Tab in search mode: ignored, or leave search mode? (Open Question 15) | Ignored | 2026-10-08 |
| 20 | Window shown: back to the top, or the search box's pre-search position? (Open Question 16) | Back to the top | 2026-10-08 |
| 21 | Enter in an empty search box right after a show: insert the first emoji, or nothing? (Open Question 17) | Inserts the first emoji | 2026-10-08 |
| 22 | Page Up / Down and Tab while the box has the focus (Open Question 18) | Only ↓ leaves the box | 2026-10-08 |
| 23 | Implementation order: after or before the search box? (Open Question 19) | After the search box | 2026-10-08 |
| 24 | Who aligns the search box workfile? (Open Question 20) | This session | 2026-10-08 |

---

*Last updated: 2026-10-08*
