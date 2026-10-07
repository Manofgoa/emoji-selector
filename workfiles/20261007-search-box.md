# Search Box

> Working document — a search box at the top of the window, focused whenever the window opens, to
> find an emoji by typing its name or a keyword, in English or in French.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

A **search box** sits at the top of the window, above the category tabs. Every time the window
opens it is **cleared and focused**, so the user types right away. As soon as it holds text, the
**tabs are greyed** and the grid shows one flat **Search results** list, **ranked by relevance**,
instead of its category sections; emptying it brings the category view back where it was.

Keywords come from Emojibase, in **English and French at once**: `cat` and `chat` both find 🐱,
with no setting.

Reference: Twitter's emoji picker (screenshot shared by the user) — tabs greyed while searching, a
*Résultats de recherche* header over a flat list; `caca` finds 💩 (*caca*) and 🥜 (*cacahuète*).

| In scope | Out of scope |
|---|---|
| Search box, focused and cleared on every show | Recents tab, skin tones, flags tab ([TODO-FEATURES.md](TODO-FEATURES.md)) |
| English and French keywords (label + tags) | Arrow-key navigation in the grid ([TODO-FEATURES.md](TODO-FEATURES.md) *Keyboard navigation*) |
| Relevance ranking | A language setting — both languages are always searched |
| Greyed tabs and flat result list while searching | A custom-drawn, Twitter-like box (rounded, magnifier inside) |
| Enter inserts the first result, Esc clears / hides | Unit tests — checked by hand (Q&A #13) |

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
| No test project | `src/` |

---

## Keyword Data

- **Second data file**: `fr/compact.json` of the same version (17.0.0), kept as published, embedded
  next to the English one (`Data/Emojibase/`, renamed per locale — e.g. `compact.en.json`,
  `compact.fr.json`). The exe grows by ≈ 600 KB.
- **Join**: the French entries are matched to the English ones by `hexcode`. The English file stays
  the master list (categories, order, skipped groups); a French entry with no English match is
  ignored, an English entry with no French match keeps its English keywords only.
- **Keywords** of an emoji, each tagged **name** or **tag**:
  - *name*: the English `label` and the French `label`;
  - *tag*: the English `tags` and the French `tags`.

  `Emoji` gains them, pre-normalized and split into words (see *Matching*). The shown name
  (`Emoji.Name`) stays the English label — the app's UI is in English.
- `CONTRIBUTING.md § Emoji data` describes fetching both files.

---

## Matching

- **Normalization**, on both the typed text and the keywords: lower case, **diacritics removed**
  (`é` → `e`, `ç` → `c`), punctuation and spaces are separators. A keyword is split into **words**
  (`tête de chat` → `tete`, `de`, `chat`; `sentir mauvais` → `sentir`, `mauvais`).
- **Typed words**: the text is split into words; an emoji is a **result** when **every typed word is
  contained** in at least one word of its keywords, in either language — anywhere in the word, not
  only at its start (`huet` finds 🥜 *cacahuète*).
- **Live**: the results update at every keystroke — about 1 900 emojis, no delay needed.

### Relevance (tiers)

A typed word's match against one keyword word is graded, best first:

| Criterion | Order |
|---|---|
| 1. **Tier** | *exact* (the whole word) → *start* (the word begins with it) → *inside* (anywhere else) |
| 2. **Coverage** | typed length ÷ word length, higher first — `caca` covers 100 % of *caca*, 44 % of *cacahuete* |
| 3. **Source** | *name* before *tag* |

A typed word's grade for an emoji is its **best** match among that emoji's keyword words.

**Ranking of the results**:

1. Single typed word: by its grade (tier, then coverage, then source).
2. Several typed words: by the **worst tier** among them, then the **lowest coverage**, then the
   number of words matched in a *name* (more first).
3. Ties keep the **catalog order** (Win+; order) — a stable sort.

Examples: `caca` → 💩 (*caca*, tag, exact) before 🥜 (*cacahuete*, tag, start, 44 %); `chat` → 🐱
(*tête de chat*, name, exact) before 🐈 if *chat* is only one of its tags.

---

## Window and Search Mode

### Search box

- A native WinForms **`TextBox`**, docked at the top **above the tab strip**, DPI-scaled like the
  other controls, placeholder **`Search emojis`** (`PlaceholderText`).
- A **✕ clear button** next to it, shown only while the box holds text: empties the box and gives
  the focus back to it.

### Show behaviour

- Every time the window becomes visible (`OnVisibleChanged`, whatever the show path), the search
  box is **cleared** — which restores the category view — and **gets the focus**.

### Keys in the box

| Key | Does |
|---|---|
| Enter | Inserts the **first result** into the previous window, like a click on it (the window hides). Nothing when the box is blank or there is no result |
| Esc | Box holds text → **clears** it. Box empty → **hides the window** to the tray, like the close button |

### Search mode (the box holds non-blank text)

| Element | Behaviour |
|---|---|
| Tab strip | Every tab **greyed**, no active underline, no hover, **clicks ignored** — a new state of `CategoryTabStrip` (the built-in `Enabled` does not grey custom drawing) |
| Grid | One section, header **`Search results`**, holding the results in relevance order; scrolled to the top on every change of the text |
| No result | The header, then the message **`No emoji found`** |
| Emoji clicked | Inserted into the previous window, as today; the window hides |

- `EmojiGrid` gets a way to swap its sections for the result list and back; the category view's
  `ActiveCategoryChanged` is not raised while searching.
- **Leaving search mode** (box emptied, by typing, ✕ or Esc): tabs enabled again, category view
  back **at the scroll position it had before the search** (the active tab follows). On a show,
  the box is cleared too, so the view also returns there.

---

## Documentation

| File | Change |
|---|---|
| `RULES.md` | New section: the search box, the keys, the matching and relevance rules, the show behaviour |
| `README.md` / `README.fr.md` | The search in the features |
| `CONTRIBUTING.md` | § Emoji data: both locale files |
| `GLOSSARY.md` / `GLOSSARY.fr.md` | *Keyword*: its name or one of its tags, **in English or in French** |

---

## Test Impact

**Nothing testable changes in a test file**: the app has no test project and none is created
(Q&A #13) — every behaviour is checked by hand on the running app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none (checked by hand: `caca` → 💩 before 🥜, `chat` / `cat` → 🐱, accents ignored, Enter / Esc, greyed tabs, scroll restored, focus on show) | — | — |

---

## Open Questions

- [x] ~~1. Matching rule: start of a word, or a substring anywhere?~~ → Substring anywhere (*contains*), ranked: start of a word is worth more, and the share of the word covered counts (`caca` exact beats *cacahuète*)
- [x] ~~2. Result order: catalog or relevance?~~ → Relevance, name matches before tag matches
- [x] ~~3. Keyboard in the box?~~ → Enter inserts the first result; Esc clears the box, or hides the window when it is empty
- [x] ~~4. Look of the box?~~ → Native `TextBox`, placeholder, ✕ button next to it
- [x] ~~5. UI texts?~~ → `Search emojis`, `Search results`, `No emoji found`
- [x] ~~6. Leaving search mode?~~ → Back where the grid was before the search
- [x] ~~7. Greyed tab clicked?~~ → Ignored
- [x] ~~8. Unit tests?~~ → By hand, no test project
- [x] ~~9. Backlog row?~~ → Marked with this workfile
- [x] ~~10. How do the relevance criteria combine?~~ → Tiers: exact → start → inside, then coverage, then name before tag, then catalog order

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

### Iteration 2 — 2026-10-08

Open Questions answered (Q&A #5–#14):

- **Matching** changed from word-prefix to *contains*, with a **relevance ranking** the user
  described: a match at the start of a word is worth more, and the share of the word covered counts
  (`caca` vs *caca* = 100 % beats `caca` vs *cacahuète*). Combined as **tiers** (exact → start →
  inside, then coverage, then name before tag, then catalog order). Keywords are now tagged
  *name* / *tag*.
- Ranking of **several typed words** (worst tier, then lowest coverage, then name matches) is the
  agent's extension of the tiers to multi-word queries — to be read by the user before the go.
- Enter inserts the first result; Esc clears, or hides when empty.
- Native `TextBox` with a ✕ button; texts `Search emojis` / `Search results` / `No emoji found`.
- Leaving search mode restores the previous scroll position; greyed tabs ignore clicks.
- No unit tests: Test Impact emptied, explicitly.
- *Search box* row of `TODO-FEATURES.md` marked with this workfile.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable — no test project, checked by hand (Q&A #13) |
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
| 5 | Open Questions 1–9, asked in plain text | User asked for them as multiple choice instead (#6–#14) | 2026-10-07 |
| 6 | Q1 — Matching rule: start of a word or substring? | *Contains*, but a match at the start is worth more, and the word's length matters: `caca` vs *caca* is 100 % of the letters, vs *cacahuète* less → *caca* comes first | 2026-10-08 |
| 7 | Q2 — Result order: catalog or relevance? | Relevance | 2026-10-08 |
| 8 | Q3 — Keys in the box? | Enter inserts the first result; Esc clears, or hides when empty | 2026-10-08 |
| 9 | Q4 — Look of the box: native or custom-drawn? | Native `TextBox` | 2026-10-08 |
| 10 | Q5 — UI texts `Search emojis` / `Search results` / `No emoji found`? | As proposed | 2026-10-08 |
| 11 | Q6 — Leaving search mode: back where it was, or to the top? | Back where it was | 2026-10-08 |
| 12 | Q7 — Greyed tab clicked: ignored, or clears and jumps? | Ignored | 2026-10-08 |
| 13 | Q8 — Unit tests: xUnit project or by hand? | By hand | 2026-10-08 |
| 14 | Q10 — Combine the relevance criteria: tiers or weighted score? Q9 — Mark the backlog row? | Tiers; mark the row | 2026-10-08 |

---

*Last updated: 2026-10-08*
