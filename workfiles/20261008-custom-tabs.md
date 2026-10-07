# Custom Tabs

> Working document — tabs the user creates, names and gives an emoji icon, filled with the emojis
> of their choice by right click, reordered through a "…" menu on their section.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The user can create **several custom tabs**, each with a **name** and an **icon emoji** of their
choice. Emojis are **added** to a custom tab and **removed** from it by **right click**. Each custom
tab is one more tab in the tab strip and one more section of the continuous grid; its section header
carries a **"…" button** whose menu offers **Reorder**, a mode where the emojis of that section — and
of that section only — are moved by drag and drop.

| In scope | Out of scope |
|---|---|
| Creating, naming, renaming, deleting custom tabs | Custom tabs in the search results (see *Search*) |
| An icon emoji per tab, chosen by the user, drawn in colour in the tab strip | Reordering the catalog categories or the frequent tab |
| Adding / removing an emoji by right click | Keyboard navigation inside the reorder mode ([20261007-keyboard-navigation.md](20261007-keyboard-navigation.md) is in design) |
| The "…" menu of a custom section, its *Reorder* mode (drag and drop inside the section) | |
| Saving the custom tabs in a file | |

### Starting point

| Fact | Source |
|---|---|
| A category is `EmojiCategory(Name, char Icon, Emojis)`; `Icon` is a Segoe Fluent Icons code point, not an emoji | `Data/EmojiCategory.cs:8` |
| The 7 tabs are a fixed list built once by `EmojiCatalog.Load()`; the **same** list goes to `EmojiGrid`, `CategoryTabStrip` and `EmojiSearch.Find`, tab index = section index | `Data/EmojiCatalog.cs`, `UI/MainForm.cs:62-64` |
| Both controls treat the list as immutable: no "categories changed" entry point | `UI/EmojiGrid.cs:33-59`, `UI/CategoryTabStrip.cs:45` |
| Tab glyphs are drawn with `TextRenderer` in the icon font: accent when active, `GrayText` otherwise, a blend when greyed — a colour emoji cannot go that way | `UI/CategoryTabStrip.cs` (`OnPaint`) |
| Tabs have a fixed logical width; the strip's minimum width — and the window's `MinimumSize`, set once — follow the number of tabs; the drag area is what is left after the last tab | `UI/CategoryTabStrip.cs` (`LogicalMinimumWidth`, `TabBounds`, `IsDragArea`), `UI/MainForm.cs` |
| Bitmaps are pre-rendered for the whole catalog and looked up **by emoji text** (`EmojiBitmapCache.TryGet`): an emoji shown in a custom section or as a tab icon reuses its bitmap, nothing more to render | `Drawing/EmojiBitmapCache.cs` |
| Section headers are plain text (`TextRenderer`, bold); no per-section button, `EmojiGridLayout.HitTest` returns cells only | `UI/EmojiGrid.cs` (`OnPaint`), `UI/EmojiGridLayout.cs` |
| The grid handles the **left** click only (`OnMouseClick` → `EmojiClicked`); no right click, no `ContextMenuStrip`, no drag code. The tab strip handles the left click only | `UI/EmojiGrid.cs:341-348`, `UI/CategoryTabStrip.cs:283-290` |
| The settings menu is a `ContextMenuStrip` owned by `MainForm`, shown under its button | `UI/MainForm.cs:260-267` |
| No user data is persisted today; the `cache\` folder next to the exe is written best effort (temporary file then move, failures ignored) | `Drawing/EmojiBitmapCache.cs:77, 222-241` |
| The **frequent tab** workfile (designed, implementation under way in the `frequent-tab` worktree) adds a first tab built from counters saved in `usage.json` next to the exe, kept out of the search | [20261008-frequent-tab.md](20261008-frequent-tab.md) |
| No test project — every earlier workfile checked by hand, by the user's decision | [20261007-category-tabs.md](20261007-category-tabs.md) Q&A #13 |

---

## Custom Tabs

- **Several**, created, named, renamed and deleted by the user (Q&A #1).
- Each one has a **name** — the tab's tooltip and the section header — and an **icon emoji** chosen by
  the user, drawn **in colour** in the tab strip from the pre-rendered bitmap (Q&A #3).
- A custom tab is one more `EmojiCategory` in the list given to the tab strip and the grid; the
  record gains what a custom tab needs (its icon emoji, a custom marker).
- The list of tabs becomes **mutable**: the tab strip and the grid get an entry point to rebuild
  after a creation, a deletion, an addition or a removal.

## Right-Click Menu

- **Add**: right click on an emoji → an item adding it to a custom tab (Q&A #2).
- **Remove**: right click on an emoji **in a custom section** → *Remove* (Q&A #2).
- The grid raises a new event for the right click (section, emoji, location); `MainForm` builds and
  shows the menu, like the settings menu.

## Section "…" Menu and Reorder Mode

- A custom section's header carries a **"…" button** on its right; the header text is shortened so
  its ellipsis does not run under the button. Catalog and frequent sections have none.
- Its menu offers **Reorder**, which turns on a **drag-and-drop mode inside that section only**
  (Q&A #2) — custom sections only.
- The drag is hand-rolled inside the grid (mouse down / move / up, an insertion marker painted in
  `OnPaint`), not OLE drag and drop: nothing leaves the section. While dragging, the hover and the
  selection that `OnMouseMove` drives are suspended.

## Storage

- To decide (see *Open Questions*).

## Search

- To decide (see *Open Questions*).

---

## Documentation

| File | Change |
|---|---|
| `GLOSSARY.md` / `GLOSSARY.fr.md` | The term for a custom tab (see *Open Questions*) |
| `RULES.md` | The custom tabs: creation, the right-click menu, the "…" menu, the reorder mode, the file |
| `README.md` / `README.fr.md` | The feature |

---

## Test Impact

No test project exists, and the user decided against one in the earlier workfiles: the behaviours
below are **checked by hand** in the running app, not by unit tests.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| A created custom tab shows in the tab strip with its icon emoji in colour, and as a section of the grid | — (manual) | — |
| Right click → add puts the emoji in the custom tab; right click → remove takes it out | — (manual) | — |
| *Reorder* moves emojis by drag and drop inside its section only; a click does not insert meanwhile | — (manual) | — |
| Rename, change of icon, deletion are reflected in the strip and the grid | — (manual) | — |
| The custom tabs come back after a restart; a missing or invalid file, or a read-only folder → no error | — (manual) | — |

---

## Open Questions

- [x] ~~How many custom tabs?~~ → Several, created, named, renamed and deleted by the user
- [x] ~~How is a custom tab's content managed?~~ → Add and remove by right click; reorder through a "…" menu on the custom section, whose *Reorder* item turns on drag and drop inside it
- [x] ~~Which icon in the tab strip?~~ → An emoji chosen by the user
- [ ] Which glossary term — and what becomes of the existing *Favorite* term?
- [ ] Where is a custom tab created from?
- [ ] How does the user choose a tab's icon emoji?
- [ ] Where are *Rename* and *Delete* offered?
- [ ] Where do the custom tabs sit in the tab strip?
- [ ] What happens when the tabs no longer fit in the strip?
- [ ] Does deleting a tab ask for a confirmation?
- [ ] What does an empty custom tab show?
- [ ] How does the reorder mode end, and what does a click do while it is on?
- [ ] What does the right-click "add" item look like with several custom tabs?
- [ ] Where is the file saved?
- [ ] Is this implemented after the frequent tab is merged?
- [ ] Are the custom sections searched?
- [ ] How does a colour icon show the active and the greyed (search) states?
- [ ] Can the custom tabs themselves be reordered in the strip?
- [ ] Which constraints on a tab's name?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-08

Initial design from the request ("create a custom tab and put the emojis of one's choice in it")
and the scoping batch (Q&A #1–4): several user-managed tabs, an icon emoji chosen by the user,
add and remove by right click, a "…" menu on the custom section whose *Reorder* item turns on drag
and drop inside it. No row of `TODO-FEATURES.md` matches the request. Codebase explored with two
scout passes (the subject being straightforward): tab structure / grid sections / mouse handling,
and persistence / tests. Sixteen questions left open, front-loaded.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable — no test project, checked by hand |
| README | | | |
| RULES.md, glossary | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | How many custom tabs can the user have? | Several, managed by the user (create, name, rename, delete) | 2026-10-08 |
| 2 | How does the user manage a custom tab's content? | Add by right click, remove by right click; to reorder, a "…" menu in the custom section with a *Reorder* item turning on drag and drop inside that section (custom sections only) | 2026-10-08 |
| 3 | Which icon does a custom tab show in the tab strip? | An emoji chosen by the user | 2026-10-08 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward | 2026-10-08 |
| 5 | Which glossary term, and what becomes of *Favorite*? | | |
| 6 | Where is a custom tab created from? | | |
| 7 | How does the user choose a tab's icon emoji? | | |
| 8 | Where are *Rename* and *Delete* offered? | | |
| 9 | Where do the custom tabs sit in the tab strip? | | |
| 10 | What happens when the tabs no longer fit in the strip? | | |
| 11 | Does deleting a tab ask for a confirmation? | | |
| 12 | What does an empty custom tab show? | | |
| 13 | How does the reorder mode end, and what does a click do meanwhile? | | |
| 14 | What does the right-click "add" item look like? | | |
| 15 | Where is the file saved? | | |
| 16 | Implemented after the frequent tab is merged? | | |
| 17 | Are the custom sections searched? | | |
| 18 | How does a colour icon show the active and greyed states? | | |
| 19 | Can the custom tabs be reordered in the strip? | | |
| 20 | Which constraints on a tab's name? | | |

---

*Last updated: 2026-10-08*
