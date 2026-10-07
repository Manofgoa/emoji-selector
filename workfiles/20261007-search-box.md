# Search Box

> Working document — a search box at the top of the window, focused whenever the window opens, to
> find an emoji by typing its name or a keyword, in English or in French.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

A **search box** sits at the top of the window, above the category tabs. Every time the window
opens it is **cleared and focused**, so the user types right away. As soon as it holds text, the
**tabs are greyed** and the grid shows one flat **Search results** list instead of its category
sections; emptying it brings the category view back.

Keywords come from Emojibase, in **English and French at once**: `cat` and `chat` both find 🐱,
with no setting.

Reference: Twitter's emoji picker (screenshot shared by the user) — rounded box with a magnifier
and a ✕ clear button, tabs greyed while searching, a *Résultats de recherche* header over a flat
list; `caca` finds 🥜 (*cacahuète*) and 💩 (*caca*), so a typed word matches the **start** of a
word.

| In scope | Out of scope |
|---|---|
| Search box, focused and cleared on every show | Recents tab, skin tones, flags tab ([TODO-FEATURES.md](TODO-FEATURES.md)) |
| English and French keywords (label + tags) | Keyboard navigation in the grid ([TODO-FEATURES.md](TODO-FEATURES.md)) — see Open Questions for Enter / Esc |
| Greyed tabs and flat result list while searching | A language setting — both languages are always searched |

### Starting point

| Fact | Source |
|---|---|
| The embedded data is `en/compact.json` only (emojibase-data **17.0.0**, ≈ 570 KB): `label`, `tags`, `group`, `order`, `unicode`, `hexcode`, `skins` | `src/EmojiSelector/Data/Emojibase/compact.json`, csproj `EmbeddedResource` |
| `EmojiCatalog.Load()` reads only `unicode`, `label`, `group`, `order` — tags are dropped | `Data/EmojiCatalog.cs` |
| `fr/compact.json` exists upstream at the same version: ≈ 600 KB, 1 949 entries, 1 923 with tags (`1F95C` *cacahuètes* → `cacahuète`, `1F4A9` → `caca`) | `https://cdn.jsdelivr.net/npm/emojibase-data@17.0.0/fr/compact.json`, checked 2026-10-07 |
| Records: `Emoji(Text, Name)` inside `EmojiCategory(Name, Icon, Emojis)` | `Data/Emoji.cs` |
| `MainForm` adds `grid` (`Dock.Fill`) then `tabStrip` (`Dock.Top`); it has no `Shown` / `Activated` / `VisibleChanged` handling — the window comes back through `OnTrayIconClicked` | `UI/MainForm.cs` |
| `CategoryTabStrip` is custom-drawn (GDI) with no disabled state | `UI/CategoryTabStrip.cs` |
| `EmojiGridLayout` takes a list of section counts — one flat section is `[n]`; `EmojiGrid` indexes `categories[section]` everywhere (paint, hit test, header text) | `UI/EmojiGridLayout.cs`, `UI/EmojiGrid.cs` |
| The grid and the tab strip are not selectable: clicking them does not take the focus | `ControlStyles.Selectable = false` |
| Insertion depends on `ForegroundTracker`, not on this window's focus — a focused text box does not disturb it | `Input/` |
| No test project — the category tabs workfile chose to check everything by hand | [20261007-category-tabs.md](20261007-category-tabs.md) Q&A #13 |

---

## Keyword Data

- **Second data file**: `fr/compact.json` of the same version (17.0.0), kept as published, embedded
  next to the English one (`Data/Emojibase/`, renamed per locale — e.g. `compact.en.json`,
  `compact.fr.json`). The exe grows by ≈ 600 KB.
- **Join**: the French entries are matched to the English ones by `hexcode`. The English file stays
  the master list (categories, order, skipped groups); a French entry with no English match is
  ignored, an English entry with no French match keeps its English keywords only.
- **Keywords** of an emoji = its English `label` and `tags` + its French `label` and `tags`. `Emoji`
  gains them, pre-normalized for matching (see *Matching*). The shown name (`Emoji.Name`) stays the
  English label — the app's UI is in English.
- `CONTRIBUTING.md § Emoji data` describes fetching both files.

---

## Matching

Proposed — see Open Questions.

- **Normalization**, on both the typed text and the keywords: lower case, **diacritics removed**
  (`é` → `e`, `ç` → `c`), punctuation treated as a separator.
- **Rule**: the typed text is split into words; an emoji matches when **every typed word is the
  start of a word** of one of its keywords, in either language. `caca` → 🥜 (*cacahuete*) and 💩
  (*caca*); `chat` → 🐱; `smil cat` → 😺.
- **Live**: the results update at every keystroke — about 1 900 emojis, no delay needed.
- **Order**: see Open Questions.

---

## Window and Search Mode

### Search box

- A text box docked at the top, **above the tab strip**, DPI-scaled like the other controls.
- Look, placeholder, clear button: see Open Questions.

### Show behaviour

- Every time the window becomes visible (`OnVisibleChanged`, whatever the show path), the search
  box is **cleared** — which restores the category view — and **gets the focus**.

### Search mode (the box holds non-blank text)

| Element | Behaviour |
|---|---|
| Tab strip | Every tab **greyed**, no active underline, no hover, clicks ignored — a new state of `CategoryTabStrip` (the built-in `Enabled` does not grey custom drawing) |
| Grid | One section, header **`Search results`**, holding the matches; scrolled to the top on every change of the text |
| No match | Message: see Open Questions |
| Emoji clicked | Inserted into the previous window, as today; the window hides |

- `EmojiGrid` gets a way to swap its sections for the result list and back; the category view's
  `ActiveCategoryChanged` is not raised while searching.
- **Leaving search mode** (box emptied): tabs enabled again, category view back — scroll position:
  see Open Questions.

---

## Documentation

| File | Change |
|---|---|
| `RULES.md` | New section: the search box, the matching rule, the show behaviour |
| `README.md` / `README.fr.md` | The search in the features |
| `CONTRIBUTING.md` | § Emoji data: both locale files |
| `GLOSSARY.md` / `GLOSSARY.fr.md` | Only if a term changes (*Search box* and *Keyword* already exist; *Keyword* gains "in English or French") |

---

## Test Impact

Pending Open Question 8. If no test project is created, nothing testable changes in a test file:
the behaviours below are checked by hand.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| Normalization: case, diacritics, separators | `tests/EmojiSelector.Tests/Data/EmojiSearchTests.cs` (if a test project is created) | Create |
| Word-prefix match in either language (`caca` → 🥜 💩, `chat` → 🐱) | same | Create |
| Every typed word must match (`smil cat` → 😺, not 😀) | same | Create |
| French keywords joined to the right emoji by `hexcode` | `tests/EmojiSelector.Tests/Data/EmojiCatalogTests.cs` | Create |
| One flat section laid out from `[n]` | `tests/EmojiSelector.Tests/UI/EmojiGridLayoutTests.cs` | Create |

---

## Open Questions

- [ ] 1. **Matching rule**: every typed word must be the start of a word of a keyword, case- and accent-insensitive (proposed, as on Twitter) — or a plain substring anywhere (`ton` would find *bouton*, *carton*…)?
- [ ] 2. **Result order**: the catalog order (Win+; order) — or by relevance: name match first (e.g. `chat` → 🐱 *tête de chat* before 🐈‍⬛), then tag matches?
- [ ] 3. **Keyboard in the box**: Enter inserts the first result? Esc clears the box when it holds text, otherwise hides the window? (Arrow navigation stays in the backlog.)
- [ ] 4. **Look of the box**: native WinForms `TextBox` with a placeholder and a ✕ clear button next to it — or custom-drawn like Twitter (rounded border, magnifier, ✕ inside)?
- [ ] 5. **UI texts** (English, like the rest of the UI): placeholder `Search emojis`, header `Search results`, no match `No emoji found` — OK, or other wording?
- [ ] 6. **Leaving search mode** by emptying the box: the grid returns to where it was before the search — or to the top (first tab)?
- [ ] 7. **Greyed tab clicked** while searching: ignored (as on Twitter) — or clears the search and jumps to that category?
- [ ] 8. **Unit tests**: by hand like the category tabs — or create an xUnit test project now, since the matching is a pure function worth pinning?
- [ ] 9. **Backlog**: mark the *Search box* row of [TODO-FEATURES.md](TODO-FEATURES.md) with this workfile?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-07

Initial design from the scoping batch (Q&A #1–#4) and the Twitter screenshot:

- Both languages always searched: `fr/compact.json` embedded next to the English file, joined by
  `hexcode`; English label + tags and French label + tags are the keywords.
- Search box above the tabs, cleared and focused on every show (`OnVisibleChanged`).
- While searching: tabs greyed (new tab strip state), one flat `Search results` section.
- Word-prefix, accent-insensitive matching proposed, from the screenshot (`caca` → 🥜 *cacahuète*).
- Scout pass only (the user rated the subject straightforward): data and window/grid layout.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Search in English or French: how do the two languages coexist? | Both, always — no setting | 2026-10-07 |
| 2 | With text typed, how does the grid show the results? | Like Twitter: every tab greyed, the emojis filtered (screenshot: flat *Search results* list) | 2026-10-07 |
| 3 | On every reopening of the window, what becomes of the search text? | Cleared | 2026-10-07 |
| 4 | Is the subject straightforward or tricky / long? | Straightforward — scout pass only | 2026-10-07 |
| 5 | Open Questions 1–9 | | 2026-10-07 |

---

*Last updated: 2026-10-07*
