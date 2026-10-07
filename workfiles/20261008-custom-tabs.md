# Custom Tabs

> Working document — one custom tab holding the user's custom groups: groups the user creates and
> names, filled with the emojis of their choice by right click, reordered through a "…" menu on
> their section.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

A new **custom tab**, with a fixed glyph, sits right after the frequent tab. Its content is the
user's **custom groups**: several, created from the settings menu, each with a **name** — its section
header — and its own emojis. Emojis are **added** to a group and **removed** from it by **right
click**. Each group is one section of the continuous grid; its header carries a **"…" button** whose
menu offers *Rename…*, *Reorder* — a mode where the emojis of that group, and of that group only,
are moved by drag and drop — and *Delete group*.

| In scope | Out of scope |
|---|---|
| The custom tab, one, fixed glyph, after the frequent tab | An icon per group (dropped, Q&A #10) |
| Creating (settings menu), renaming, deleting custom groups | Reordering the catalog categories or the frequent tab |
| Adding / removing an emoji by right click | Moving emojis with the keyboard in the reorder mode |
| The "…" menu of a group's section, its *Reorder* mode (drag and drop inside the group) | |
| Saving the custom groups in a file | |
| Glossary: *Custom tab* and the group term in, *Favorite* out | Hiding a group or the custom tab ([20261008-frequent-tab-toggle.md](20261008-frequent-tab-toggle.md)) |

### Starting point

| Fact | Source |
|---|---|
| A category is `EmojiCategory(Name, char Icon, Emojis)`; `Icon` is a Segoe Fluent Icons code point | `Data/EmojiCategory.cs:8` |
| The 7 tabs are a fixed list built once by `EmojiCatalog.Load()`; the **same** list goes to `EmojiGrid`, `CategoryTabStrip` and `EmojiSearch.Find`, and **tab index = section index** (`TabClicked` → `grid.SelectCategory(index)`, `ActiveCategoryChanged` → `tabStrip.ActiveTab`) | `Data/EmojiCatalog.cs`, `UI/MainForm.cs:62-64, 83, 88` |
| Both controls treat the list as immutable: no "categories changed" entry point | `UI/EmojiGrid.cs:33-59`, `UI/CategoryTabStrip.cs:45` |
| Tab glyphs are drawn with `TextRenderer` in the icon font: accent when active, `GrayText` otherwise, a blend when greyed | `UI/CategoryTabStrip.cs` (`OnPaint`) |
| Bitmaps are pre-rendered for the whole catalog and looked up **by emoji text** (`EmojiBitmapCache.TryGet`): an emoji shown in a custom group reuses its bitmap, nothing more to render | `Drawing/EmojiBitmapCache.cs` |
| Section headers are plain text (`TextRenderer`, bold); no per-section button, `EmojiGridLayout.HitTest` returns cells only | `UI/EmojiGrid.cs` (`OnPaint`), `UI/EmojiGridLayout.cs` |
| The grid already paints a message in an empty section (`No emoji found`, one cell high under the header) | `UI/EmojiGrid.cs` (`NoResultText`) |
| The grid handles the **left** click only (`OnMouseClick` → `EmojiClicked`); no right click, no `ContextMenuStrip`, no drag code | `UI/EmojiGrid.cs:341-348` |
| The settings menu is a `ContextMenuStrip` owned by `MainForm`, shown under its button; it holds *Open app folder* | `UI/MainForm.cs:260-267` |
| No user data is persisted today; the `cache\` folder next to the exe is written best effort (temporary file then move, failures ignored) | `Drawing/EmojiBitmapCache.cs:77, 222-241` |
| The **frequent tab** is **merged** into `main` (`dfb66ff`): the list is built once as `[CreateFrequentCategory(), .. categories]`; the grid swaps the frequent section in place (`ReplaceCategory(0, …)`); counters in `usage.json` (`Data/EmojiUsage.cs`, the file pattern to follow); *Clear frequently used* in the settings menu | [20261008-frequent-tab.md](20261008-frequent-tab.md), `UI/MainForm.cs`, `UI/EmojiGrid.cs` |
| The **keyboard navigation** is merged too: arrows move the selection in the grid, Enter inserts it | [20261007-keyboard-navigation.md](20261007-keyboard-navigation.md) |
| The **frequent tab toggle** workfile (in design) waits for this one: it **reuses** the "…" button of the section headers for the frequent section, and applies its hiding to the custom groups | [20261008-frequent-tab-toggle.md](20261008-frequent-tab-toggle.md) |
| No test project — every earlier workfile checked by hand, by the user's decision | [20261007-category-tabs.md](20261007-category-tabs.md) Q&A #13 |

---

## Custom Tab

- **One** tab in the strip for every custom group (Q&A #10), with a **fixed monochrome glyph**, drawn
  like the other tabs: the **heart**, `EB51` (Heart) in Segoe Fluent Icons / Segoe MDL2 Assets
  (Q&A #22). Its tooltip is its name, `Custom`.
- **Position**: right **after the frequent tab**, before *Smileys & People*; its groups are the
  sections right after the frequent section in the continuous grid (Q&A #9).
- **One tab, several sections**: today a tab is one section. The custom tab covers **all** the group
  sections — a click on it scrolls to the first group; it is the active tab while the section at the
  top of the grid is one of the groups. The tab ↔ section mapping, today an identity, becomes a
  lookup.
- **No group yet**: the tab is **shown** all the same; it stands for one section, `Custom`, reading
  **`Create a group from ⚙ → New group…`**, painted like `No emoji found` (Q&A #23). It goes away
  with the first group and comes back with the deletion of the last one.
- The list of sections becomes **mutable**: the grid gets an entry point to rebuild after a group is
  created, renamed, deleted, or an emoji added, removed or moved.

## Custom Groups

- Glossary term: **Custom group** (*groupe personnalisé*) (Q&A #21).
- **Several**, created, named, renamed and deleted by the user (Q&A #1, #10).
- **Created** from the **settings menu** ⚙ — a *New group…* item next to *Open app folder* — which
  asks for the group's name (Q&A #6). A new group comes **last**.
- **Name** (*New group…* and *Rename…*): trimmed, **non-blank** — *OK* greyed while blank; duplicates
  allowed, no length limit — a long one ends with an ellipsis in the header (Q&A #20).
- **Order** of the groups: the user's, changed by *Move up* / *Move down* (Q&A #19).
- Each group has a **name**, its section header, and its own **ordered list of emojis**; no icon
  (Q&A #10).
- **Empty** group: its section shows **`Right-click an emoji to add it here`**, one cell high under
  the header, painted like `No emoji found` (Q&A #12).
- **Delete group** asks first when the group holds emojis — a Yes / No message box, *No* the
  default, like *Clear frequently used*; an empty group is deleted without a question (Q&A #11).

## Right-Click Menu

- **Add**: right click on an emoji — in any section, search results included → **`Add to ▸`**, a
  submenu listing every group in order; the groups already holding the emoji are **checked**, and a
  click on a checked group **removes** it from that group (Q&A #14). One group → still a submenu.
  No group → the item is greyed.
- **Remove**: right click on an emoji **in a custom group** → *Remove* (Q&A #2), taking it out of
  that group.
- An emoji is **at most once** in a group; it may be in several groups.
- The grid raises a new event for the right click (section, emoji, location); `MainForm` builds and
  shows the menu, like the settings menu.

## Section "…" Menu and Reorder Mode

- A group's section header carries a **"…" button** on its right; the header text is shortened so
  its ellipsis does not run under the button. Catalog and frequent sections have none **in this
  workfile** — the button is a property of a section, not hard-wired to the groups, since the
  frequent tab toggle gives it to the frequent section next.
- Its menu: **Rename…**, **Reorder**, **Move up**, **Move down**, **Delete group** (Q&A #8, #19) —
  the only place offering them. *Move up* / *Move down* swap the group with its neighbour, greyed on
  the first / last group.
- **Reorder** turns on a **drag-and-drop mode inside that group only** (Q&A #2) — custom groups only.
- **While it is on** (Q&A #13): the group's header shows a **`Done`** button in place of "…"; a click
  on an emoji of the group **inserts nothing** — the mouse only drags; each drop is **saved at once**.
  **Enter** on the group's selected emoji inserts nothing either; the arrows still move the
  selection, never the emoji (Q&A #24).
- **It ends** on *Done*, on **Esc**, and when the window **hides**.
- The drag is hand-rolled inside the grid (mouse down / move / up, an insertion marker painted in
  `OnPaint`), not OLE drag and drop: nothing leaves the section. While dragging, the hover and the
  selection that `OnMouseMove` drives are suspended.

## Storage

- **`custom-groups.json`, next to the exe** (`AppContext.BaseDirectory`), like the frequent tab's
  `usage.json` (Q&A #15). A file, not a folder: the kebab-case folder rule does not apply.
- **Format**: the groups in order, each with its name and its emojis in order, the emojis as their
  **text** (readable in the file):

  ```json
  [
    { "name": "Work", "emojis": ["👍", "✅", "🚀"] },
    { "name": "Family", "emojis": ["❤️", "😘"] }
  ]
  ```

- **Read** once at launch. Missing → no group. Unreadable or invalid → no group, not an error (and
  overwritten at the next change).
- **Written** after each change, the whole file, through a temporary file then a replace. A folder
  that cannot be written is not an error: the groups live in memory until the app ends.
- An emoji in the file that the catalog no longer has is **kept in the file** and not shown.

## Search

- The search box searches the **catalog categories only**, like the frequent tab: the custom groups
  would otherwise give their emojis twice in the results (Q&A #17).

## Delivery Order

- Implemented **after the frequent tab is merged** into `main` (Q&A #16): both change the list of
  tabs, `MainForm` and the settings menu; starting from it puts the custom tab right after the
  frequent one and avoids the conflicts. **Satisfied**: the frequent tab is in `main` (`dfb66ff`).
- The [frequent tab toggle](20261008-frequent-tab-toggle.md) comes after this one.

---

## Documentation

| File | Change |
|---|---|
| `GLOSSARY.md` / `GLOSSARY.fr.md` | New terms **Custom tab** (*onglet personnalisé*) and **Custom group** (*groupe personnalisé*); **Favorite** removed (Q&A #5, #21) |
| `RULES.md` | The custom tab and its groups: creation, the right-click menu, the "…" menu, the reorder mode, the file |
| `README.md` / `README.fr.md` | The feature |

---

## Test Impact

No test project exists, and the user decided against one in the earlier workfiles: the behaviours
below are **checked by hand** in the running app, not by unit tests.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| The custom tab sits after the frequent tab; a click scrolls to the first group; it is active while a group is at the top | — (manual) | — |
| *New group…* in the settings menu creates a named group, shown as a section with the help message | — (manual) | — |
| With no group, the custom tab is shown and its section points to *New group…* | — (manual) | — |
| Right click → `Add to ▸` puts the emoji in the group, checked afterwards; a click on a checked group or *Remove* takes it out | — (manual) | — |
| *Reorder* moves emojis by drag and drop inside its group only; neither a click nor Enter inserts meanwhile; *Done*, Esc and hiding end it | — (manual) | — |
| A search never returns an emoji twice (custom groups not searched) | — (manual) | — |
| *Rename…* changes the header (blank name refused); *Delete group* asks when the group holds emojis, not when empty | — (manual) | — |
| *Move up* / *Move down* swap the group with its neighbour, greyed at the ends | — (manual) | — |
| The groups come back after a restart; a missing or invalid file, or a read-only folder → no error | — (manual) | — |

---

## Open Questions

- [x] ~~How many custom tabs?~~ → Several, created, named, renamed and deleted by the user *(revised 2026-10-08, see Iteration 3: one custom tab, several groups)*
- [x] ~~How is a custom tab's content managed?~~ → Add and remove by right click; reorder through a "…" menu on the custom section, whose *Reorder* item turns on drag and drop inside it
- [x] ~~Which icon in the tab strip?~~ → An emoji chosen by the user *(revised 2026-10-08, see Iteration 3: one fixed glyph)*
- [x] ~~Which glossary term — and what becomes of the existing *Favorite* term?~~ → *Custom tab* (*onglet personnalisé*), *Favorite* removed
- [x] ~~Where is a custom tab created from?~~ → The settings menu ⚙ *(now: a group, see Iteration 3)*
- [x] ~~How does the user choose a tab's icon emoji?~~ → Right click → *Use as tab icon* *(revised 2026-10-08, see Iteration 3: no icon per group)*
- [x] ~~Where are *Rename* and *Delete* offered?~~ → The "…" menu of the section only
- [x] ~~Where do the custom tabs sit in the tab strip?~~ → After the frequent tab, before the categories
- [x] ~~What happens when the tabs no longer fit in the strip?~~ → Moot: one custom tab holds every group
- [x] ~~Does deleting a tab ask for a confirmation?~~ → Yes when the group holds emojis, *No* the default; an empty one without a question
- [x] ~~What does an empty custom tab show?~~ → A help message in its section
- [x] ~~Which term for a group (*Custom group* / *groupe personnalisé*)?~~ → *Custom group* (*groupe personnalisé*)
- [x] ~~Which glyph for the custom tab?~~ → The heart
- [x] ~~What does the custom tab show while no group exists?~~ → Shown, with a message pointing to ⚙ → *New group…*
- [x] ~~How does the reorder mode end, and what does a click do while it is on?~~ → *Done* in the header, Esc, or the window hiding; a click inserts nothing meanwhile, each drop saved at once
- [x] ~~What does the right-click "add" item look like with several groups?~~ → An `Add to ▸` submenu, the groups holding the emoji checked (a click on one removes it)
- [x] ~~Where is the file saved?~~ → `custom-groups.json` next to the exe
- [x] ~~Is this implemented after the frequent tab is merged?~~ → Yes, after the merge
- [x] ~~Are the custom groups searched?~~ → No, the catalog only
- [x] ~~Can the groups themselves be reordered?~~ → *Move up* / *Move down* in the "…" menu; a new group comes last
- [x] ~~Which constraints on a group's name?~~ → Trimmed and non-blank only: duplicates allowed, no length limit
- [x] ~~The keyboard navigation is now merged: in the reorder mode, what does Enter on the selected emoji of the group do?~~ → Nothing, like the click; the arrows still move the selection
- [x] ~~How does a colour icon show the active and the greyed (search) states?~~ → Moot: a fixed monochrome glyph

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

### Iteration 2 — 2026-10-08

Q&A #5–8 answered: the term *Custom tab* (*onglet personnalisé*), *Favorite* removed from the
glossary; a tab created from the settings menu; its icon chosen by right click → *Use as tab icon*
on one of its emojis; *Rename* and *Delete* offered in the section's "…" menu only.

### Iteration 3 — 2026-10-08

Q&A #9–12 answered, with a change of structure (#10): **one** custom tab in the strip, with a fixed
glyph, holding **several custom groups**, each with its name and its emojis. The icon per tab
(Iteration 2, #7) is dropped, and the overflow and colour-icon questions with it. What Iterations 1–2
said of a "custom tab" now applies to a **group**: created from the settings menu (*New group…*),
renamed and deleted from its section's "…" menu. Also settled: the custom tab right after the
frequent tab; *Delete group* asks only when the group holds emojis; an empty group shows a help
message. New questions: the group term, the tab's glyph, the tab with no group.

### Iteration 4 — 2026-10-08

Q&A #13–17 and #21–23 answered: the term *Custom group* (*groupe personnalisé*); the heart glyph;
the tab shown even with no group, with a message pointing to *New group…*; the reorder mode ended by
*Done*, Esc or hiding, a click inserting nothing meanwhile; an `Add to ▸` submenu with the groups
holding the emoji checked; `custom-groups.json` next to the exe; the custom groups kept out of the
search; the implementation waiting for the frequent tab to be merged. Two questions left: reordering
the groups, the constraints on a name.

### Iteration 5 — 2026-10-08

Q&A #19–20 answered: the groups are reordered by *Move up* / *Move down* in their "…" menu, a new
group coming last; a group's name is only trimmed and non-blank — duplicates allowed, no length
limit. No open question left.

### Iteration 6 — 2026-10-08

Starting point refreshed: the frequent tab and the keyboard navigation were merged into `main`
meanwhile, and the new frequent tab toggle workfile waits for this one, reusing the "…" button for
the frequent section — so the button is a property of a section, not hard-wired to the groups. The
delivery order (Q&A #16) is satisfied: the frequent tab is already in `main`. One question emerges
from the keyboard navigation: what Enter does in the reorder mode.

### Iteration 7 — 2026-10-08

Q&A #24 answered: in the reorder mode, Enter on the group's selected emoji inserts nothing, like the
click; the arrows still move the selection. No open question left.

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
| 5 | Which glossary term, and what becomes of *Favorite*? | *Custom tab* (*onglet personnalisé*), *Favorite* removed | 2026-10-08 |
| 6 | Where is a custom tab created from? | The settings menu ⚙ | 2026-10-08 |
| 7 | How does the user choose a tab's icon emoji? | Right click → *Use as tab icon* (the default ⭐ at creation) | 2026-10-08 |
| 8 | Where are *Rename* and *Delete* offered? | The section's "…" menu only | 2026-10-08 |
| 9 | Where do the custom tabs sit in the tab strip? | After the frequent tab, before the categories | 2026-10-08 |
| 10 | What happens when the tabs no longer fit in the strip? | One single tab stands for the custom ones — no need for several icons; each **group** has its label and its own set of emojis. The icon choice is cancelled: one fixed glyph by default | 2026-10-08 |
| 11 | Does deleting a tab ask for a confirmation? | Yes when it holds emojis (Yes / No, *No* the default); an empty one without a question | 2026-10-08 |
| 12 | What does an empty custom tab show? | A help message in its section | 2026-10-08 |
| 13 | How does the reorder mode end, and what does a click do meanwhile? | A *Done* button in the header in place of "…", plus Esc and the window hiding; a click inserts nothing, each drop saved at once | 2026-10-08 |
| 14 | What does the right-click "add" item look like? | An `Add to ▸` submenu, the groups holding the emoji checked (click → removes); greyed with no group | 2026-10-08 |
| 15 | Where is the file saved? | `custom-groups.json` next to the exe | 2026-10-08 |
| 16 | Implemented after the frequent tab is merged? | Yes, after the merge | 2026-10-08 |
| 17 | Are the custom groups searched? | No, the catalog only | 2026-10-08 |
| 18 | ~~How does a colour icon show the active and greyed states?~~ | Not asked — moot after #10 | 2026-10-08 |
| 19 | Can the groups be reordered? | *Move up* / *Move down* in the "…" menu | 2026-10-08 |
| 20 | Which constraints on a group's name? | Non-blank only (trimmed; duplicates allowed, no length limit) | 2026-10-08 |
| 21 | Which term for a group? | *Custom group* (*groupe personnalisé*) | 2026-10-08 |
| 22 | Which glyph for the custom tab? | The heart | 2026-10-08 |
| 23 | What does the custom tab show while no group exists? | Shown, with a message pointing to ⚙ → *New group…* | 2026-10-08 |
| 24 | In the reorder mode, what does Enter on the selected emoji of the group do? | Nothing, like the click | 2026-10-08 |

---

*Last updated: 2026-10-08*
