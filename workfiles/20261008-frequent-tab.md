# Frequent Tab

> Working document — a first tab, not a catalog category, listing the emojis used most, each use
> counted in a file next to the exe.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Every time an emoji is used, its **counter** goes up by one, saved in `usage.json` next to the exe.
A new first tab, **Frequently used** (a star), shows the emojis used most, the most used first, as a
section at the top of the continuous grid — a "fake" tab: its emojis come from the counters, not
from `Data/EmojiCatalog.cs`.

| In scope | Out of scope |
|---|---|
| Counting every use, saved in `usage.json` next to the exe | Recents sorted by date (replaced by this tab, see *Backlog*) |
| The *Frequently used* tab and section, first, star glyph | Skin tones, flags ([TODO-FEATURES.md](TODO-FEATURES.md)) |
| The empty state (a message in the section) | Search results ranking the frequent emojis first (Q&A #11) |
| Every show scrolled to the top, on the frequent section | The tray icon starting on the last emoji used — it stays 😊, never persisted (Q&A #11) |
| A *Clear frequently used* item in the settings menu | |
| A new glossary term, **Frequent** (*fréquent*) | |

### Starting point

| Fact | Source |
|---|---|
| A category is `EmojiCategory(Name, Icon, Emojis)`; `CategoryTabStrip` and `EmojiGrid` both take the list of categories, one tab and one section each | `Data/EmojiCategory.cs`, `UI/MainForm.cs` |
| `EmojiGridLayout` takes one emoji count per section; a section of 0 emojis has 0 rows (header only) | `UI/EmojiGridLayout.cs` |
| The grid already paints a message in an empty section: `No emoji found` in search mode, one cell high, under the header | `UI/EmojiGrid.cs` (`NoResultText`) |
| Bitmaps are looked up **by emoji text** (`EmojiBitmapCache.TryGet`): an emoji shown in a second section reuses its pre-rendered bitmap, nothing more to render | `Drawing/EmojiBitmapCache.cs` |
| Every use goes through `MainForm.InsertEmoji` → `OnEmojiUsed` — click in the grid and Enter in the search box alike | `UI/MainForm.cs` |
| The search box searches the list of categories it is given (`EmojiSearch.Find(categories, text)`) | `Data/EmojiSearch.cs` |
| The folder of the exe is `AppContext.BaseDirectory` (used by the `cache\` folder) | `Drawing/EmojiBitmapCache.cs` |
| Every show clears the search; `ShowCategories` restores the scroll position the category view had | `UI/MainForm.cs`, `UI/EmojiGrid.cs` |
| No test project — every earlier workfile checked by hand | [20261007-category-tabs.md](20261007-category-tabs.md) Q&A #13 |
| The keyboard navigation workfile is in design: Tab / Shift+Tab move between sections | [20261007-keyboard-navigation.md](20261007-keyboard-navigation.md) |

---

## Usage Counters

- **What counts**: every **use** of an emoji — every call of `MainForm.OnEmojiUsed`, the one place
  already telling the tray icon (a click in the grid, Enter in the search box, and any later way of
  inserting). One use → its counter **+1**, and its **last use** set to now.
- **Storage**: `usage.json`, **next to the exe** (`AppContext.BaseDirectory`), not in `cache\` —
  that folder is disposable, the counters are not. A file, not a folder: the kebab-case folder rule
  does not apply.
- **Format**: a JSON object keyed by the emoji's **text** (its Unicode sequence, readable in the
  file), each value holding its count and its last use (UTC, ISO 8601):

  ```json
  {
    "😂": { "count": 12, "lastUsed": "2026-10-08T09:14:03Z" },
    "👍": { "count": 12, "lastUsed": "2026-10-07T17:40:51Z" }
  }
  ```

- **Read** once at launch. Missing → no counter yet. Unreadable or invalid → no counter, not an
  error (and overwritten at the next use).
- **Written** after each use, the whole file, through a temporary file then a replace, so a crash
  never leaves it half-written. A folder that cannot be written is not an error: the counters live
  in memory until the app ends, like the `cache\` folder's rule.
- An emoji in the file that the catalog no longer has (a data update) is **kept in the file** and
  not shown.
- Owned by one new class, `Data/EmojiUsage.cs`: load, record a use, the sorted list, save.

---

## Frequent Tab

- **Position**: the **first tab**, before *Smileys & People*; its section is the **first section** of
  the continuous grid. It is one more `EmojiCategory` at the head of the list given to the tab
  strip and the grid — built from the counters, not from the catalog.
- **Label and glyph**: `Frequently used`, the tab's tooltip and the section header (English, like
  the rest of the UI); glyph **star**, `E734` (FavoriteStar) in Segoe Fluent Icons / Segoe MDL2
  Assets, monochrome like the others.
- **Order**: the **most used first**; equal counts → the **most recently used first**.
- **Limit**: as many emojis as **3 rows** of the grid hold — the limit
  follows the number of columns, so resizing the window shows more or fewer. The rows count lives
  in **one constant**.
- **Use count**: each emoji of the section shows its **number of uses under it**, inside its cell —
  small grey text, the emoji moved up to make room. Up to 999 as is, beyond that `999+`. Only in this
  section: the catalog sections and the search results show no count.
- **Empty** (no emoji used yet): the tab is **shown** and the section reads **`No emoji used yet`**,
  painted like `No emoji found`, one cell high under the header.
- **Refresh**: the section is rebuilt after each use. A use hides the window, so it is never seen
  changing.
- **Every show** scrolls the grid **to the top**, on the frequent section, like Win+; — after the
  search is cleared (`ShowCategories` would otherwise bring back the previous scroll position). The
  scroll position kept while searching is still restored when the box is emptied without hiding.
- **Search**: the search box searches the **catalog categories only** — the frequent section would
  otherwise give each of its emojis twice in the results.

- **Clear frequently used**: an item of the settings menu (next to *Open app folder*) resetting
  every counter. It asks first — a Yes / No message box, *No* the default — since the counters
  cannot be brought back; on *Yes*, `usage.json` is rewritten empty (`{}`) and the section shows
  `No emoji used yet`. Greyed while there is no counter.

---

## Backlog

The backlog row **Recents tab** (`TODO-FEATURES.md`) is marked with this workfile: the frequent
tab replaces it (Q&A #7).

---

## Documentation

| File | Change |
|---|---|
| `GLOSSARY.md` / `GLOSSARY.fr.md` | New term **Frequent** (*fréquent*): an emoji counted by its uses, shown in the first tab; *Recent* removed or reworded |
| `RULES.md` | The `usage.json` file (where, format, failure cases), the frequent tab (first, order, limit, empty state, not searched, every show on top), the *Clear frequently used* item |
| `README.md` / `README.fr.md` | The feature; *Recents* removed from the planned features if listed |

---

## Test Impact

No test project exists, and no earlier workfile created one: the behaviours below are **checked by
hand** in the running app, not by unit tests.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| A use adds 1 to the emoji's counter and writes `usage.json` next to the exe | — (manual) | — |
| The frequent section lists the most used first, ties broken by the last use | — (manual) | — |
| The section holds at most the agreed number of rows, following the columns on resize | — (manual) | — |
| No `usage.json` → the tab is shown, the section reads `No emoji used yet` | — (manual) | — |
| An invalid `usage.json` or a read-only folder → no error, the app runs | — (manual) | — |
| A search never returns an emoji twice | — (manual) | — |
| Every show brings the grid back to the top, on the frequent section | — (manual) | — |
| Each frequent emoji shows its use count under it; the other sections show none | — (manual) | — |
| *Clear frequently used* asks, then empties the section and `usage.json`; greyed with no counter | — (manual) | — |

---

## Open Questions

- [x] ~~Which term for the list sorted by uses?~~ → **Frequent** (*fréquent*), a new glossary term
- [x] ~~Where is the tab, which glyph?~~ → First tab, star
- [x] ~~What does the tab show before any use?~~ → The tab is shown, the section carries a message
- [x] ~~How many emojis at most?~~ → Two or three rows of the grid (the number: see *Two rows or three?*)
- [x] ~~Where is the file?~~ → `usage.json` next to the exe
- [x] ~~Tie between two counts?~~ → The most recently used first (the last use is stored)
- [x] ~~What happens to the backlog's *Recents tab* row?~~ → Marked entirely with this workfile
- [x] ~~Two rows or three?~~ → Three rows
- [x] ~~On each show, does the grid come back where it was (today's behaviour), or scrolled to the top, on the frequent section?~~ → Scrolled to the top, on the frequent section
- [x] ~~Do any of these join the scope: search results ranking the frequent emojis first; the tray icon starting on the last emoji used (read from `usage.json`) instead of 😊; a *Clear frequently used* item in the settings menu?~~ → Only the *Clear frequently used* item

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-08

Initial design from the request ("a fake favorites tab, each use +1 to a counter in a file near the
exe, sorted by number of uses") and the scoping batch (Q&A #1–8): the term *Frequent*, a first tab
with a star, an empty state with a message, a limit in grid rows, `usage.json` next to the exe,
ties broken by the last use, the backlog's *Recents tab* row marked. Codebase read directly (a
single scout pass, the subject being straightforward): the tab is one more `EmojiCategory` at the
head of the list, its bitmaps already pre-rendered (looked up by text), and the search must skip it
to avoid duplicates. Three questions left open.

### Iteration 2 — 2026-10-08

Q&A #9–11 answered: the section holds **3 rows**; every show scrolls the grid to the top, on the
frequent section; the *Clear frequently used* settings item joins the scope (with a Yes / No
confirmation, greyed with no counter), while the search ranking and the persisted tray icon stay
out. No open question left.

### Iteration 3 — 2026-10-08 — ✅ Implemented

Go given for **code, tests and documentation**, in a **worktree**
(`.claude/worktrees/frequent-tab`, branch `feature/frequent-tab`, from `main` at `a278285`). The
scope is frozen as the sections above stand. The confirmation of *Clear frequently used* (Yes / No,
*No* the default) was proposed with the go and is part of it.

### Iteration 4 — 2026-10-08 — ⚙️ Post-implementation — Use count under each emoji

Requested during the run: the frequent section shows each emoji's **number of uses under it**. The
cell keeps its size; the emoji moves up, the count is drawn below it in small grey text, `999+`
beyond 999 (the agent's choice, so that the count never overflows the cell). See *Frequent Tab*.

### Iteration 5 — 2026-10-08 — ⚙️ Post-implementation — Three rows at most, following the window

Requested during the run: the frequent emojis always fit in **3 rows at most**, their number
recomputed from the window's size. This is the design already (*Limit*, Q&A #9) and what the code
does: `EmojiGridLayout` cuts the section to `3 × Columns`, computed again at every resize — checked
at two widths. No change.

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
| 1 | The glossary already defines *Favorite* (marked by the user) and *Recent* (picked lately): which term for a list sorted by uses? | *Frequent* (*fréquent*), a new term | 2026-10-08 |
| 2 | Where does the fake tab sit, with which glyph? | First tab, star | 2026-10-08 |
| 3 | Before any use, what does the tab show? | Tab visible, the section shows a message | 2026-10-08 |
| 4 | How many emojis at most? | Two or three rows of the grid | 2026-10-08 |
| 5 | Where does the counters file live, under which name? | `usage.json` next to the exe | 2026-10-08 |
| 6 | Two emojis with the same count: which one first? | The most recently used | 2026-10-08 |
| 7 | The backlog's *Recents tab* row is partly covered: what to do with it? | Mark it entirely | 2026-10-08 |
| 8 | Is the subject straightforward or tricky / long? | Straightforward | 2026-10-08 |
| 9 | Two rows or three? | Three | 2026-10-08 |
| 10 | On each show: back where it was, or scrolled to the frequent section? | Scrolled to the top, on the frequent section | 2026-10-08 |
| 11 | Which extras join the scope (search ranking, tray icon start, clear item)? | The *Clear frequently used* item only | 2026-10-08 |

---

*Last updated: 2026-10-08*
