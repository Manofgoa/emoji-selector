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
- **Ranking**: an emoji found **only through a synonym** ranks **after** every emoji found by its own keywords.

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

- **The trigger**: a typed word **without symbol**, folded, **equal** to a word of a group — whole, not its start
  (`bag` expands nothing: one letter would expand half the dictionary). Its **alternatives**: every other word of
  every group holding it (a word in two groups gets both) — not transitive beyond that.
- **The alternatives match whole keyword words only** (`Exact`): `car` must not find `cardigan`, `carrot`.
- **Per typed word**: its own match first (characters, symbols, keywords — as today); **none** → its alternatives',
  that match marked **via synonym**. An emoji is a result when every typed word matches, directly or via a synonym.
- **Ranking**: first the emojis with **no** typed word matched via synonym, then the others; inside each, the current
  keys (worst tier, lowest coverage, name matches), a synonym match counting as `Exact`, full coverage, a tag.
- **Everywhere `Find` runs**: the search results, near mode (`n:` / `near:`), the special characters' search — all
  through `EmojiSearch.Find`, given the loaded `SearchSynonyms`.
- **Highlight** (details panel): the alternatives of each typed word are highlighted too, **as whole words**, in the
  names, tags and emoticons (`EmojiSearch.MatchSpans` given the synonyms, `EmojiDetailsPanel.Synonyms`) — so `bagnole`
  on 🚗 highlights `car`. Always, even on an emoji the typed word matched on its own.

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
| `AppReset` deletes `synonyms.json` | None — reflection check on a copy of the build folder | — |

---

## Open Questions

Every question below was decided by the agent: the user delegated the choices (Q&A 5).

- [x] ~~Does a typed word expand only when whole, or from its start too?~~ → Whole only: a start would expand on one or
  two letters.
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

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3 | 2026-11-06 | `Data/SearchSynonyms.cs`, `Data/Synonyms.json`, `EmojiSearch` (`Find`, `MatchSpans`), `MainForm`, `EmojiDetailsPanel`, `AppReset` |
| Unit tests | 3 | 2026-11-06 | No test project — 27 reflection checks on a copy of the build folder, all passing |
| RULES.md | 3 | 2026-11-06 | *Search Box* § *Synonyms*, the ranking and highlight bullets, *Reset All Settings* |
| README | 3 | 2026-11-06 | `README.md` / `README.fr.md`: the *Synonyms* bullet, the reset's file list |
| GLOSSARY | 3 | 2026-11-06 | `GLOSSARY.md` / `GLOSSARY.fr.md`: *Synonym* (*synonyme*) |

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

---

*Last updated: 2026-11-06*
