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
  next to the English one in `Data/Emojibase/`, both renamed per locale: `compact.en.json`,
  `compact.fr.json`. Both `EmbeddedResource` items carry `WithCulture="false"` — otherwise MSBuild
  reads `.en` / `.fr` as cultures and moves the data to satellite assemblies. The exe grows by
  ≈ 600 KB.
- **Join**: the French entries are matched to the English ones by `hexcode`. The English file stays
  the master list (categories, order, skipped groups); a French entry with no English match is
  ignored, an English entry with no French match keeps its English keywords only.
- **Keywords** of an emoji, each tagged **name** or **tag**:
  - *name*: the English `label` and the French `label`;
  - *tag*: the English `tags` and the French `tags`.

  `Emoji` gains them as `EmojiKeyword(Word, IsName)` records, pre-normalized and split into words,
  each word once — a name's when a name has it (see *Matching*). The shown name
  (`Emoji.Name`) stays the English label — the app's UI is in English.
- `CONTRIBUTING.md § Emoji data` describes fetching both files.

---

## Matching

- **Normalization** (`EmojiSearch.Normalize`), on both the typed text and the keywords: lower case,
  **diacritics removed** (`é` → `e`, `ç` → `c`, `œ` → `oe`, `æ` → `ae`), every character but
  letters and digits is a separator. A keyword is split into **words**
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

- A native WinForms **`TextBox`**, in a two-column `TableLayoutPanel` docked at the top **above the
  tab strip**, placeholder **`Search emojis`** — the native cue banner shown even while focused
  (`EM_SETCUEBANNER`, wParam `TRUE`): `PlaceholderText` hides on focus, and the box always has it.
- A **✕ clear button** next to it (flat, no border), shown only while the box holds text: empties
  the box and gives the focus back to it. It is a **square exactly as high as the box**, with the
  same margins, so showing it never changes the bar's height.

### Show behaviour

- Every time the window becomes visible (`OnVisibleChanged`, whatever the show path), the search
  box is **cleared** — which restores the category view — and **gets the focus**. The category view
  is then scrolled back to the **top**, its first emoji selected
  ([20261007-keyboard-navigation.md](20261007-keyboard-navigation.md), see Iteration 5).

### Keys in the box

| Key | Does |
|---|---|
| Enter | Inserts the grid's **selection** into the previous window, like a click on it (the window hides): the **first result**, unless the arrows moved the selection; the **first emoji** of the grid when the box is blank. Nothing when there is no result (see Iteration 5) |
| Esc | Box holds text → **clears** it. Box empty → **hides the window** to the tray, like the close button |
| ↓ | Hands the keyboard to the grid, on the first emoji — the rest of the keyboard navigation: [20261007-keyboard-navigation.md](20261007-keyboard-navigation.md) |

### Search mode (the box holds non-blank text)

| Element | Behaviour |
|---|---|
| Tab strip | Every tab **greyed** — halfway between the inactive grey and the background —, no active underline, no hover, no tooltip, **clicks ignored**: `CategoryTabStrip.Greyed` (the built-in `Enabled` does not grey custom drawing) |
| Grid | One section, header **`Search results`**, holding the results in relevance order; scrolled to the top on every change of the text |
| No result | The header, then the message **`No emoji found`** |
| Emoji clicked | Inserted into the previous window, as today; the window hides |

- `EmojiGrid.ShowSearchResults` swaps its sections for the result list (keeping the category
  view's scroll offset on the first call), `ShowCategories` swaps them back; the category view's
  `ActiveCategoryChanged` is not raised while searching.
- **Leaving search mode** (box emptied, by typing, ✕ or Esc): tabs enabled again, category view
  back **at the scroll position it had before the search** (the active tab follows). On a show,
  the box is cleared too, but the view goes back to the **top** instead (see *Show behaviour*).

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
- [x] ~~3. Keyboard in the box?~~ → Enter inserts the first result; Esc clears the box, or hides the window when it is empty *(revised 2026-10-08, see Iteration 5: Enter inserts the selection, the first emoji when the box is blank)*
- [x] ~~4. Look of the box?~~ → Native `TextBox`, placeholder, ✕ button next to it
- [x] ~~5. UI texts?~~ → `Search emojis`, `Search results`, `No emoji found`
- [x] ~~6. Leaving search mode?~~ → Back where the grid was before the search *(revised 2026-10-08, see Iteration 5: on a show, back to the top)*
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

### Iteration 3 — 2026-10-08 — ✅ Implemented

Go given: code and documentation (no unit tests, Q&A #13), in a **worktree**
(`.claude/worktrees/search-box`, branch `feature/search-box`, from `882c350`) — another session was
modifying `MainForm.cs` on `main` in the original checkout.

### Iteration 4 — 2026-10-08 — 🧭 Implementation choices

No rule broken. Choices the frozen design did not state:

- **Data files** named `compact.en.json` / `compact.fr.json` (the design's example). The first check
  launch crashed: MSBuild treated `.en` / `.fr` as cultures and built satellite assemblies — fixed
  with `WithCulture="false"` on both items.
- **Placeholder** through `EM_SETCUEBANNER` instead of `PlaceholderText`, which hides on focus —
  the box always has the focus, so the placeholder never showed.
- **✕ button** flat, borderless, a square as high as the box with its margins. Fixed during the
  run after the user reported the tabs moving a few pixels down when text was typed: the
  auto-sized button was taller than the box and grew the bar.
- **Greyed tabs** drawn halfway between `GrayText` and the background; hover and tooltip off too.
- **Keyword words** kept once per emoji, flagged *name* when a name has them, even if a tag also
  does.
- **API**: `EmojiKeyword(Word, IsName)`, `EmojiSearch.Find / Normalize / Words`,
  `EmojiGrid.ShowSearchResults / ShowCategories`, `CategoryTabStrip.Greyed`.
- **Checks**: ranking checked by reflection on the built DLL (`caca` → 💩, 🪿, 🥜; `chat` / `cat` →
  cats; `Cœur` = `coeur`; `huet` → 🥜; `zzzq` → none). Screens checked: placeholder, greyed tabs,
  `Search results` header, ✕. Not checked by script: Enter (it would type into a real app) and the
  restored scroll position — left to the user's test.
- **Incident**: a first scripted check sent `caca` with `SendKeys` while the window was not in
  front — the keys may have landed in another app. Rule added to `RULES.md § Search Box`: scripts
  send `WM_SETTEXT` / `WM_KEYDOWN` to the box itself.

### Iteration 5 — 2026-10-08 — ⚙️ Post-implementation — Aligned on keyboard navigation

Decided in [20261007-keyboard-navigation.md](20261007-keyboard-navigation.md) (its Q&A 17–24),
logged here by that workfile's session; the **code is changed by that workfile's run**, not here:

- **Enter** inserts the grid's **selection** rather than `searchResults[0]`: in search mode the
  selection sits on the first result after every change of the text, so Enter still inserts it
  unless the arrows moved it. In a **blank** box, Enter inserts the grid's first emoji (it did
  nothing).
- **On a show**, the category view goes back to the **top**, first emoji selected, instead of the
  scroll position it had before the search. Emptying the box while the window stays open still
  restores that position.
- **↓** in the box hands the keyboard to the grid; ↑ on the grid's first row or a typed character
  brings it back. Page Up / Down and Tab are ignored while the box has the focus; Tab is ignored in
  search mode.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 4 | 2026-10-08 | Data and search, tab strip, grid, search box; ✕ height fix after the user's report |
| Unit tests | | | Not applicable — no test project, checked by hand (Q&A #13) |
| README | 3 | 2026-10-08 | `README.md` / `README.fr.md`: *Search box* feature, removed from *Planned*; Tech line |
| RULES | 3, 4 | 2026-10-08 | New § Search Box; Enter / Esc rows in the window table |
| Glossary | 3 | 2026-10-08 | *Keyword*: in English or in French (both languages) |
| CONTRIBUTING | 3 | 2026-10-08 | § Emoji data: both locale files |

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
