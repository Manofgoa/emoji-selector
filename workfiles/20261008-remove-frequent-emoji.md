# Remove an Emoji from the Frequent Tab

> Working document — a right-click menu item taking one emoji out of the *Frequently used* section.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The **frequent tab** ([20261008-frequent-tab.md](20261008-frequent-tab.md)) lists the most used
emojis. Today the only way to take one out is **Clear frequently used**, which resets them all.
This workfile adds **one item** to the emoji's right-click menu, built by the custom tabs
([20261008-custom-tabs.md](20261008-custom-tabs.md), merged in `8b02c16`): **`Remove from
frequently used`**, forgetting that emoji's counter alone.

| In scope | Out of scope |
|---|---|
| The `Remove from frequently used` item in the emoji's right-click menu, on the frequent section | The item in any other section (Q&A #2) |
| Forgetting the emoji's counter in `usage.json` | Hiding an emoji from the tab for good, an exclusion list (Q&A #4) |
| The frequent section refreshed right away | A confirmation (Q&A #4) |
| The **Menu key / Shift+F10** opening the right-click menu on the selection, in every section (Q&A #7) | The **Delete** key (Q&A #7) |
| | The tray icon (Q&A #6 — made customizable by [20261008-custom-tray-icon.md](20261008-custom-tray-icon.md)) |

### Existing code

| Fact | Where |
|---|---|
| A right click on an emoji raises `EmojiRightClicked` (section, emoji, location) — on the mouse-up, any section | `UI/EmojiGrid.cs:560-571`, `857` |
| `MainForm.ShowEmojiMenu` builds the menu: `Add to ▸` always, `Remove` when the section is a custom group's (`GroupOf`) | `UI/MainForm.cs:613-637` |
| **In search mode the grid's only section is `Search results`, at index 0** — the frequent section's index outside search mode | `UI/EmojiGrid.cs:278` |
| The frequent section is section 0 (`FirstCustomSection = 1`); `TabOf` maps sections to tabs | `UI/MainForm.cs:88`, `572-575` |
| `EmojiUsage` has `Record`, `Clear`, `CountOf`, `MostUsed` — no way to forget one emoji | `Data/EmojiUsage.cs` |
| After a use or *Clear*, `ReplaceCategory(0, CreateFrequentCategory())` rebuilds the section; the selection goes back to the first emoji in view | `UI/MainForm.cs:442`, `529`; `UI/EmojiGrid.cs:289-297` |
| `MainForm.ProcessCmdKey` routes the keys: the grid's navigation through `grid.MoveSelection`, then Enter and Esc for both places | `UI/MainForm.cs:222-293` |
| The grid exposes `SelectedEmoji`, `IsReordering`, `IsSelectionReordered`; the selection is `(Section, Index)`, its cell from `layout.CellBounds` | `UI/EmojiGrid.cs:70`, `138-144` |
| No test project: the earlier workfiles check behaviours by hand | `20261008-custom-tabs.md` § Test Impact |

---

## Counters

- New **`EmojiUsage.Remove(string emoji)`**: drops the emoji's entry, then saves `usage.json` the
  usual way (`.tmp` then a replace; a folder that cannot be written keeps the change in memory).
  An emoji with no entry → nothing, no write.
- **Forget, not hide** (Q&A #4): used again later, the emoji comes back with a count of 1, like a
  first use.

---

## Right-Click Menu

- On an emoji of the **frequent section only** (Q&A #2), `ShowEmojiMenu` adds
  **`Remove from frequently used`** (Q&A #3, `MainForm.RemoveFrequentText`), after `Add to ▸` — the
  place `Remove` takes in a custom group.
- **Not in search mode**: the search results section is index 0 too. The test is one helper,
  `MainForm.IsFrequentSection(int section)` — section 0 and the search box blank — so the frequent
  tab toggle ([20261008-frequent-tab-toggle.md](20261008-frequent-tab-toggle.md), go given, run
  deferred), which can take the frequent section out, adapts one place.
- **No confirmation** (Q&A #4): one counter, one click.
- Clicked → `usage.Remove(emoji)`, then `grid.ReplaceCategory(0, CreateFrequentCategory())`: the
  next emoji moves up into the three rows, or the section reads `No emoji used yet` when it was the
  last. The selection goes back to the first emoji in view, as after a use.
- The window stays shown.
- **The tray icon is left alone** (Q&A #6): it is being made customizable in
  [20261008-custom-tray-icon.md](20261008-custom-tray-icon.md); removing a frequent emoji does not
  touch it.

---

## Keyboard

The **Menu key** (`Keys.Apps`) and **Shift+F10** open the right-click menu **on the selection**
(Q&A #7) — the same menu, so `Remove from frequently used` on a frequent emoji, `Add to ▸` and
`Remove` everywhere else they show.

- **Grid focused**: `ProcessCmdKey` calls a new **`EmojiGrid.OpenSelectionMenu()`**: the selected
  cell is scrolled into view (the mouse wheel may have moved it out), then `EmojiRightClicked` is
  raised with the selection's section and emoji, the location at the **bottom-left corner of its
  cell** — `MainForm.ShowEmojiMenu` stays the one place building the menu. No selection → nothing.
- **Search box focused**: the keys keep the **box's own menu** (Cut, Copy, Paste…) (Q&A #8) —
  the emoji menu opens from the grid only.
- Opened from the keyboard, the menu's **first enabled item is highlighted**, as Windows does for a
  menu opened by the keyboard, so ↓ / Enter work at once.
- The menu closed, the keyboard is back where it was (WinForms gives the focus back to the control
  that had it).
- In the **reorder mode**, it opens like the right click does there.

---

## Documentation

| File | Change |
|---|---|
| `RULES.md` | The *Window and Tray Icon* table's right-click row, *Frequent Tab*: the item, what it forgets; *Keyboard*: the Menu key / Shift+F10 rows |
| `README.md` / `README.fr.md` | The *Frequently used* bullet: the right click's item; the *Keyboard* bullet: the Menu key / Shift+F10 |

---

## Test Impact

No test project exists, and the user decided against one in the earlier workfiles: the behaviours
below are **checked by hand** in the running app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| Right click on a frequent emoji → `Remove from frequently used` after `Add to ▸`; it disappears, the next one moves up, `usage.json` no longer has it | — (manual) | — |
| The last frequent emoji removed → `No emoji used yet` | — (manual) | — |
| No such item on an emoji of a custom group, a catalog category or the search results | — (manual) | — |
| A removed emoji used again → back with a count of 1 | — (manual) | — |
| Menu key / Shift+F10 in the grid → the selection's menu under its cell, first item highlighted; on a frequent emoji, `Remove from frequently used` works from the keyboard | — (manual) | — |
| Menu key with the selection scrolled out of view → scrolled back, then the menu | — (manual) | — |

---

## Open Questions

- [x] ~~The tray icon shows the last emoji used: when that emoji is removed from the frequent tab,
  does the icon keep it, or go back to 😊?~~ → Ignored here: the tray icon is made customizable in
  another workfile
- [x] ~~A keyboard way to remove the selected frequent emoji — the Menu key / Shift+F10, Delete — or
  mouse only?~~ → The Menu key / Shift+F10 open the right-click menu on the selection
- [x] ~~In the **search box**, do the Menu key / Shift+F10 keep the box's own menu, or open the
  emoji menu on the selection?~~ → The box's own menu

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-08

Initial design, from the user's request and the scoping answers (Q&A #1-5). The first scoping
batch, asked before the custom tabs were merged, was dismissed; the user asked for questions
matching the updated `main` (Q&A #1), where the right-click menu already exists — the question of
how two menus fit together fell away. One `Remove from frequently used` item, on the frequent
section only, forgetting the counter without confirmation. Found while exploring: the search
results section shares index 0 with the frequent section, hence `IsFrequentSection`. No row of
`TODO-FEATURES.md` matches the request.

### Iteration 2 — 2026-10-08

Open questions answered (Q&A #6-7). The tray icon is out of scope: another session makes it
customizable. The Menu key and Shift+F10 open the right-click menu on the selection —
`EmojiGrid.OpenSelectionMenu`, the menu still built by `ShowEmojiMenu` alone; first item
highlighted. One question emerged: what those keys do in the search box.

### Iteration 3 — 2026-10-08

Q&A #8: in the search box, the Menu key and Shift+F10 keep the box's own menu; the emoji menu opens
from the grid only. No open question left.

### Iteration 4 — 2026-10-08 — ✅ Implemented

Go given: code, manual checks and documentation, in a worktree
(`.claude/worktrees/remove-frequent-emoji`, branch `feature/remove-frequent-emoji`, from `main` at
`13a4721`). Since the design, `main` merged the **frequent tab toggle** (`13a4721`: the frequent
section can be hidden, `FirstCustomSection` is now `showFrequent ? 1 : 0`) and the **custom tray
icon** (`e7ffd8d`: `Use as tray icon` then a separator head the emoji menu). The design holds:
`IsFrequentSection` tests `showFrequent` as well, and the new item still comes after `Add to ▸`.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project — manual checks (see *Test Impact*) |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Scoping batch on the pre-merge code (shared right-click menu, where the item shows, effect, depth) | Dismissed — "refresh your questions against the updated code in `main`" | 2026-10-08 |
| 2 | Where does the item show in the right-click menu? | On the frequent section only | 2026-10-08 |
| 3 | Its label? | `Remove from frequently used` | 2026-10-08 |
| 4 | What does removing do? | Forgets the counter, no confirmation; used again → back with a count of 1 | 2026-10-08 |
| 5 | Straightforward or tricky / long? | Straightforward — one scout pass | 2026-10-08 |
| 6 | Tray icon when its emoji is removed: kept, or back to 😊? | Ignored — the tray icon is made customizable in another session | 2026-10-08 |
| 7 | A keyboard way to remove (Menu key / Shift+F10, Delete), or mouse only? | The Menu key / Shift+F10 | 2026-10-08 |
| 8 | In the search box, the Menu key / Shift+F10: the box's own menu, or the emoji menu? | The box's own menu | 2026-10-08 |

---

*Last updated: 2026-10-08*
