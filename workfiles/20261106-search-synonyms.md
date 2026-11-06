# Search Synonyms

> Working document — a dictionary of synonyms widening the search box: a typed word also finds the emojis of its
> equivalent words.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The search box finds an emoji by the words of its names and tags (Emojibase's, English and French). A word the data
does not hold finds nothing, or only loosely related emojis: `bagnole` finds no car, `mdr` no laughing face. A
**synonym dictionary** lists **groups of equivalent words** (`car, auto, voiture, bagnole`): a typed word belonging to
a group also looks for the other words of that group.

- **Shape**: groups of words (word → words), not word → emojis — one group covers every emoji holding any of its
  words.
- **Source**: `synonyms.json` next to the exe, **the user's to edit**, written from a default embedded in the exe —
  the model of `special-characters.json`.
- **Ranking**: an emoji found **only through a synonym** goes to a **`Synonyms` section** after the search results,
  never mixed with the emojis found by their own keywords; a typed word's start expands too (`bag` → `bagnole`).
- **Details panel**: the emoji's synonyms in a row of their own, below its real names and tags, italic grey.

Components: `Data/SearchSynonyms.cs` (new: the file, its loading, the lookup), `Data/Synonyms.json` (new: the embedded
default), `Data/EmojiSearch.cs` (`Find`, `MatchSpans`), `UI/MainForm.cs` (loading, warning, the search calls),
`UI/EmojiDetailsPanel.cs` (highlight), `Data/AppReset.cs`.

---

## The File

- **`synonyms.json`, next to the exe** (`SearchSynonyms.FileName`): `{ "groups": [ ["car", "auto", "voiture",
  "bagnole"], … ] }` — a group is an array of words, English and French mixed: the keywords are both languages at
  once. Comments and trailing commas are read, like `special-characters.json`.
- **Its default** is embedded (`Data/Synonyms.json`, `EmbeddedResource`, `WithCulture="false"`): **83 groups** of
  everyday words Emojibase lacks or spreads over other emojis — familiar French and English (`bagnole`, `mdr`, `lol`,
  `kiffer`, `osef`), feelings (`heureux` / `content` / `joyeux`), abbreviations (`ok`, `thx` / `merci`), everyday
  things, animals, weather. Every group holds at least one word some emoji has as a keyword word (checked against the
  embedded catalog).
- **At launch** (`SearchSynonyms.Load`), exactly as the special characters:

  | The file | Does |
  |---|---|
  | Missing | Written from the embedded default (`.tmp` then a replace), then used |
  | Valid | Used as it is — **never rewritten**, even when the exe ships a newer default |
  | Invalid — not JSON, or no `groups` list | **Kept as it is**; the embedded default used in memory; a **warning** on the window's first show |
  | Unreadable, or a folder that cannot be written | The embedded default used in memory — never an error |

- **The warning**: the same text as the special characters' (`MainForm.InvalidSpecialText`, the file's name in it),
  `synonyms.json is not valid: … The default list is used until it is fixed.` — once, from `OnShown`. Both files
  invalid → one posted call showing the two boxes **one after the other**, the special characters' first.
- A UTF-8 byte order mark (Notepad's) is skipped before parsing.
- **Lenient inside a valid file**: a group that is not an array of strings is left out; an entry is **one word**
  once normalized (`EmojiSearch.Words`: letters and digits, case and diacritics ignored) — an entry giving none or
  several (`pomme de terre`, `:)`) is left out; a group left with fewer than two words is left out; a word given twice
  in a group counts once.
- An edit is read at the **next launch**; `Open app folder` finds the file. No settings item.
- `Reset all settings…` deletes it and its temporary file: the default comes back at the restart; the confirmation
  lists *the synonyms — back to the default list*.

---

## Search

- **The trigger**: a typed word **without symbol**, folded, of **at least 2 letters** (`SearchSynonyms.MinPrefixLength`),
  that **starts** a word of a group — or equals it: `bag` and `bagnole` both start `bagnole`. Its **alternatives**:
  every word of every group holding such a word (`bag` → `car`, `auto`, `voiture`, `bagnole`, `tuture`, and the words
  of a group holding `bague` too) — not transitive beyond that. One letter expands nothing.
- **The alternatives match whole keyword words only** (`Exact`): `car` must not find `cardigan`, `carrot`.
- **Per typed word**: its own match first (characters, symbols, keywords — as before); **none** → its alternatives',
  that match marked **via synonym**. An emoji is a result when every typed word matches, directly or via a synonym.
- **Two lists** (`EmojiSearch.Find` returns `SearchResults(Direct, ViaSynonyms)`): **direct** — every typed word
  matched on its own —, then **via synonyms** — at least one typed word matched only through its alternatives. Each
  ranked by the current keys (worst tier, lowest coverage, name matches), a synonym match counting as `Exact`, full
  coverage, a tag; an emoji is in one list only.
- **Two sections** in search mode (`EmojiGrid.ShowSearchResults(direct, viaSynonyms)`): `Search results`, as before —
  `No emoji found` when empty, **kept** even when the second section has emojis —, then **`Synonyms`**
  (`EmojiGrid.SynonymsHeader`), only while it holds an emoji. `bag` → 👜 alone in the results, 🚗 in `Synonyms`. The
  selection's rule is unchanged: the grid's first emoji — the first synonym when the results are empty.
- **Near mode** (`n:` / `near:`): its matches are the direct ones then the synonyms', one list — the jump order.
- **The special characters**: searched with the catalog, their synonym matches in the same `Synonyms` section.
- **Highlight** (details panel): the alternatives of each typed word are highlighted too, **as whole words**, in the
  names, tags and emoticons (`EmojiSearch.MatchSpans` given the synonyms, `EmojiDetailsPanel.Synonyms`) — so `bag`
  on 🚗 highlights `car`. Always, even on an emoji the typed word matched on its own. In the synonyms row (below), the
  typed words **alone** are highlighted, as the search matches them — `bag` in `bagnole` —, never their alternatives:
  they are the row's own words, every one would be.
- **No group in search mode**: `MainForm.GroupOf` is null while the results are shown — the `Synonyms` section, the
  second, must never be taken for the first custom group (a `Remove` entry in its emojis' menu).

---

## Details Panel — the Synonyms Row

- **What**: the emoji's **synonyms** (`SearchSynonyms.SynonymsOf(Emoji)`): for each of its keyword words that is a
  word of a group, the other words of that group — minus the words already among its keyword words, each once, in the
  file's order. 🚗 (*car*, *voiture*…) → `auto, bagnole, tuture`. Computed once per emoji (a `ConditionalWeakTable`):
  the height runs over the whole catalog.
- **A row of its own**, **last**, under the English and French rows, after the same thin line: a **`≈`** in place of a
  flag, then the synonyms joined with `, `, **italic**, in `SystemColors.GrayText` (`Glass.GrayText` on Acrylic),
  wrapped like the tags — never at the level of a name. No bold name.
- **Always** shown while the emoji has synonyms, whatever the search; none → no row, no line.
- **Highlight**: the search's matches, in `highlightColor`, as in the other rows.
- **Height**: `EmojiDetailsPanel.HeightFor` counts the row for the emoji with the most text, as it counts the French
  row — moving the selection never moves the grid.
- Mixed languages (the groups mix English and French), so one row, not one per language; shown whether or not the
  French row is.

---

## Test Impact

The app has **no unit test project** (`src/` holds `EmojiSelector` only). The pure parts are checked by reflection on
the built dll — of a **copy** of the build folder (`APP_CONTEXT_BASE_DIRECTORY`), as `RULES.md` describes for the
special characters —, the window by a capture.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| `SearchSynonyms.Parse`: groups read, entries normalized, multi-word / symbol entries and one-word groups left out, invalid JSON / no `groups` rejected | None — reflection check on the dll | — |
| `SearchSynonyms.Load`: missing file written from the default, invalid file kept and reported | None — reflection check on a copy of the build folder | — |
| `EmojiSearch.Find`: a synonym finds the emoji, ranked after the direct results; whole-word alternatives only; no expansion on a partial word | None — reflection check on the dll | — |
| `EmojiSearch.MatchSpans`: the alternatives highlighted as whole words | None — reflection check on the dll | — |
| `SearchSynonyms.AlternativesOf`: a typed start of 2 letters or more expands (`bag` → `car`), one letter does not | None — reflection check on the dll | — |
| `EmojiSearch.Find`: two lists, `bag` → 👜 direct, 🚗 via synonyms, no emoji in both | None — reflection check on the dll | — |
| `SearchSynonyms.SynonymsOf`: 🚗 → `bagnole`…, its own keyword words left out | None — reflection check on the dll | — |
| The `Synonyms` section and the synonyms row on screen | None — capture of the window (`WM_SETTEXT` into the box, `PrintWindow`) | — |
| `AppReset` deletes `synonyms.json` | None — reflection check on a copy of the build folder | — |

---

## Open Questions

Every question below was decided by the agent: the user delegated the choices (Q&A 5).

- [x] ~~Does a typed word expand only when whole, or from its start too?~~ → Whole only: a start would expand on one or
  two letters. *(revised 2026-11-06, see Iteration 4: from its start, 2 letters at least)*
- [x] ~~Do the alternatives match keywords like typed words (contained) or whole words only?~~ → Whole words only:
  `car` would find `cardigan`.
- [x] ~~Multi-word or symbol entries (`pomme de terre`, `:)`)?~~ → Left out: a keyword is split into words, a
  multi-word entry could never match whole; symbols are the characters' and emoticons' business.
- [x] ~~Highlight the words found via a synonym in the details panel?~~ → Yes, as whole words: otherwise the panel shows
  no reason for the result.
- [x] ~~What does the default list hold, and how big?~~ → A few dozen groups of common words Emojibase lacks, English
  and French, written by the agent; the user edits the file.
- [x] ~~A settings item to open the file?~~ → No: `Open app folder` finds it, as for `special-characters.json`.
- [x] ~~Read at the next launch, or watched for changes?~~ → Next launch, like `special-characters.json`.
- [x] ~~Do synonyms apply in near mode and to the special characters?~~ → Yes: both go through `EmojiSearch.Find`.

Raised by Iteration 4:

- [x] ~~How many letters before a typed word expands from the start of a group's word (`bag` → `bagnole`)?~~ → 2
  (Q&A 6).
- [x] ~~No direct result but some through synonyms: is the empty `Search results` section (`No emoji found`) kept above
  the `Synonyms` one?~~ → Kept (Q&A 7).
- [x] ~~Where do the emoji's synonyms go in the details panel: a row of their own, or after the tags of each language
  row?~~ → A row of their own, last, `≈`, italic grey (Q&A 8).
- [x] ~~When does the details panel show them: always, only while searching, or behind a settings item?~~ → Always
  (Q&A 9).

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-06

Scoping answered by the user (Q&A 1–4): groups of equivalent words, an editable JSON file with an embedded default,
synonym results after the direct ones, a straightforward subject (one scout pass). The scout pass found the search in
`EmojiSearch` alone — `Find` serves the search results, near mode and the special characters — and the model of an
editable file with an embedded default in `SpecialCharacters`. The remaining questions were delegated to the agent
(Q&A 5) and decided as listed under *Open Questions*.

### Iteration 2 — 2026-11-06 — ✅ Implemented

The go given with the delegation (Q&A 5): *Code, unit tests and documentation*, in a **worktree**
(`.claude/worktrees/search-synonyms`, branch `feature/search-synonyms`).

### Iteration 3 — 2026-11-06 — 🧭 Implementation choices

No rule broken. Choices the frozen design did not state:

- **Scope of the go**: "start the development" read as *Code, unit tests and documentation* — the scope every run of
  this app has taken (RULES.md, README and GLOSSARY in both languages).
- **Tests**: no test project — 27 reflection checks on a copy of the build folder (`Parse`, `Load`, `Find`,
  `MatchSpans`, `AppReset`), all passing; the README's examples spot-checked (`mdr` → 😂, `thx` → 🙏, `bagnole` → 🚗).
- **Warnings**: the special characters' warning and the synonyms' are shown from one posted call, one box after the
  other — two separate posts would stack two boxes at once. `MainForm.InvalidSpecialText` reused, its name kept.
- **BOM**: a UTF-8 byte order mark is skipped before parsing (`JsonDocument` is given the bytes after it).
- **Default list**: 83 groups; checked against the catalog, none useless; groups whose every word is already some
  emoji's keyword are kept — they still cross-link emojis (`chut` finds those tagged `secret`).
- **Highlight**: the alternatives are highlighted on every emoji, the ones matched directly included — the simplest
  rule, and what the search sees.
- **Lookup**: `SearchSynonyms.AlternativesOf(word)` on the typed word's folded form, only for a word without symbol;
  `EmojiSearch.Find` and `MatchSpans` take the synonyms as an optional last argument (`null` → no synonym).
- **End of run**: no merge question — the user asked for no interaction; the branch waits for the user's word.

### Iteration 4 — 2026-11-06 — ⚙️ Post-implementation — Prefix synonyms, a Synonyms section, synonyms in the details panel

The user's request, after trying the delivery:

- **`bag` should find the cars**: a typed word starting a group's word (`bag` → `bagnole`) expands too — Iteration 1's
  "whole only" revised.
- **A section of their own**: the emojis found through a synonym go to a separate `Synonyms` section, after the
  results, so they never mix with the real ones (`bag` → 👜, *sac*, stays alone in the results).
- **In the details panel**: the emoji's synonyms listed with its terms and highlighted by the search, but **not at the
  same level** as its real names and tags.

Four questions raised (see *Open Questions*), asked as Q&A 6–9 and answered: a start of **2 letters** expands, the
empty `Search results` section is **kept** above `Synonyms`, the panel's synonyms in a **row of their own**, **always**
shown. Designed by the agent around them: `Find` returning two lists, the `Synonyms` section shown only while it holds
an emoji, near mode keeping one list (direct then synonyms), the row's content (`SynonymsOf`: the emoji's keyword
words' groups, its own words left out), `≈` in place of a flag, the alternatives still highlighted as whole words in
the real tags.

### Iteration 5 — 2026-11-06 — 🧭 Implementation choices

No rule broken. Choices the frozen design did not state:

- **The synonyms row's highlight**: the typed words alone, not their alternatives — with them, `bag` highlighted the
  whole row of 🚗 (`bagnole`, `tuture`, `auto`), the alternatives being the row's own words. Found on the capture.
- **`MainForm.GroupOf` null in search mode**: the `Synonyms` section is the grid's second, the index of the first
  custom group while the frequent tab shows — its emojis' menu would have offered `Remove`. The guard also covers the
  results section with the frequent tab hidden, which had the same flaw before this iteration.
- **`AlternativesOf` by binary search** on the sorted group words, the groups kept in the file's order: the
  alternatives and `SynonymsOf` come out in that order. `SynonymsOf` cached per emoji, by reference.
- **The panel's fonts**: a third one, the italic; the word-width cache keyed by the font's style, the three row fonts
  differing by it. The `≈` in the regular font, grey, in the flags' column; the synonyms wrapped after the names'
  indent.
- **Ties in `Synonyms`** keep the catalog order, as everywhere: `bagnole` puts 🚃 (*railway car*) before 🚗.
- **Tests**: 41 reflection checks on a copy of the build folder, all passing — the prefix (`bag`, `ba`, not `b`), the
  two lists (`bag`: 👜 direct, 🚗 via synonyms, none in both; the direct list unchanged by the synonyms for `auto`,
  `chat`, `heureux`, `mdr content`, `bag`), near mode's order, `SynonymsOf`, the row's highlight; two captures of the
  window (`bag`, `bagnole`).

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 5 | 2026-11-06 | `Data/SearchSynonyms.cs`, `Data/Synonyms.json`, `EmojiSearch` (`Find`, `MatchSpans`), `MainForm`, `EmojiDetailsPanel`, `AppReset`; then the prefix, `Data/SearchResults.cs`, the `Synonyms` section (`EmojiGrid`), the synonyms row |
| Unit tests | 3, 5 | 2026-11-06 | No test project — 27, then 41 reflection checks on a copy of the build folder, all passing; two window captures |
| RULES.md | 3, 5 | 2026-11-06 | *Search Box* § *Synonyms*, the ranking and highlight bullets, *Reset All Settings*; then search mode's sections, the prefix, the *Details Panel*'s synonyms row and height |
| README | 3, 5 | 2026-11-06 | `README.md` / `README.fr.md`: the *Synonyms* bullet, the reset's file list; then the prefix, the section, the panel's line |
| GLOSSARY | 3, 5 | 2026-11-06 | `GLOSSARY.md` / `GLOSSARY.fr.md`: *Synonym* (*synonyme*); then the prefix and the section |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Shape of the dictionary: groups of words, word → emojis, or both? | Groups of words (word → words) | 2026-11-06 |
| 2 | Where does it come from, who edits it? | An editable JSON file next to the exe, written from an embedded default | 2026-11-06 |
| 3 | Where does an emoji found only through a synonym rank? | After the direct results | 2026-11-06 |
| 4 | Straightforward or tricky / long to explore? | Straightforward | 2026-11-06 |
| 5 | (Not asked: the user, leaving) — the remaining choices and the go | "Make the choices and start the development in a worktree, without interactions — GO" | 2026-11-06 |
| 6 | Minimum letters before a typed word expands from a group word's start? | 2 letters | 2026-11-06 |
| 7 | No direct result: keep the empty `Search results` section above `Synonyms`? | Keep it | 2026-11-06 |
| 8 | Details panel: the synonyms in a row of their own, or after each row's tags? | A row of their own | 2026-11-06 |
| 9 | Details panel: synonyms shown always, only while searching, or behind a setting? | Always | 2026-11-06 |
| 10 | Go for Iteration 4: scope, and where? | *Code, unit tests and documentation*, in the current worktree | 2026-11-06 |

---

*Last updated: 2026-11-06*
