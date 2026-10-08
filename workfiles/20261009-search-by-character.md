# Search by Character

> Working document — find the emojis that show a character (`?` → ❓, `1` → 1️⃣, `a` → 🅰️) by typing
> that character, and the emojis that have an emoticon (`:)` → 🙂) by typing it.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Some emojis **are** a character: a keycap (1️⃣, #️⃣), a punctuation mark (❓, ‼️), a math sign (➕,
➗), a letter button (🅰️, 🆗, ℹ️). Typing that character in the search box must find them, ranked
first.

Today it does not, or not well:

- **Symbols are erased.** `EmojiSearch.Normalize` turns every character that is neither a letter nor
  a digit into a space, on the typed text and on the keywords alike: `?` typed gives no word at all,
  hence no result — although Emojibase gives ❓ the tag `?`, ➕ the tag `+`, ➗ `÷`, ✅ `✓`…
- **Digits and letters are found, but drowned.** `keycap: 1` already gives 1️⃣ the word `1`, and
  🅰️'s name `A button` the word `a` — but as an ordinary whole-word match, tied with every other
  emoji holding that word and kept in catalog order: the keycaps, in Symbols, come last.

Agreed scope (step 5 scoping, Q&A 1–3):

| Family | Examples |
|---|---|
| Keycaps | 0️⃣–9️⃣, #️⃣, *️⃣ by their character; 🔟 by `10` |
| Punctuation and math | ❓❔ `?`, ❗❕ `!`, ‼️ `!!`, ⁉️ `!?`, ➕➖✖️➗🟰, 💲 `$`, ©️ ®️ ™️… |
| Letters | 🅰️🅱️🅾️🅿️, ℹ️ `i`, Ⓜ️ `m`, 🆎 `ab`, 🆑 `cl`, 🆗 `ok`… |
| Emoticons | `:)` → 🙂, `<3` → ❤️ — Emojibase's `emoticon`, already shown in the details panel |

- **Ranked first**: a typed word that **is** one of an emoji's characters puts that emoji above every
  other result — a new tier, above the whole word.
- **Shown in the details panel**: an emoji's characters are added to its **tags**, so they are seen,
  and highlighted when the search finds them.

Components: `Data/EmojiSearch.cs` (matching, ranking, `MatchSpans`), `Data/EmojiCatalog.cs` (building
an emoji's keywords and tags), `Data/Emoji.cs`, a new home for the characters, `UI/EmojiDetailsPanel.cs`
(nothing to change if the characters are tags), README / RULES / GLOSSARY.

---

## Characters

An emoji's **characters** are the text it shows, as one types it: `1` for 1️⃣, `?` for ❓, `!?` for
⁉️, `OK` for 🆗. Most emojis have none; a few have two (➖ `-` and `−`).

### The list

**One list written by hand** (Q&A 5), every emoji in it — not computed from the keycap's base
character nor from NFKC: one explicit place to read and to extend. Keyed by the emoji's text as the
catalog gives it. An emoji the catalog does not have is ignored.

| Emoji | Characters | Note |
|---|---|---|
| 0️⃣ … 9️⃣ | `0` … `9` | Already a word of the name (`keycap: 1`) |
| #️⃣ *️⃣ | `#`, `*` | Erased today |
| 🔟 | `10` | Already a word of the name |
| ❓ ❔ | `?` | Already a tag, erased |
| ❗ ❕ | `!` | Already a tag, erased |
| ‼️ | `!!` | Already a tag, erased |
| ⁉️ | `!?` | Already a tag, erased |
| ➕ | `+` | Already a tag, erased |
| ➖ | `-`, `−` | Already tags, erased |
| ✖️ | `×`, `x` | `×` a tag erased, `x` a tag |
| ➗ | `÷` | Already a tag, erased |
| 🟰 | `=` | Missing from Emojibase |
| 💲 | `$` | Missing from Emojibase |
| ©️ ®️ | `©`, `®` | French tags only, erased |
| ™️ | `™`, `TM` | `tm` already a tag |
| ✔️ | `✓` | Already a tag, erased |
| 〰️ | `~` | Missing |
| 🅰️ 🅱️ 🅾️ 🅿️ | `A`, `B`, `O`, `P` | Already words |
| 🆎 | `AB` | Already a word |
| ℹ️ | `i` | Already a tag |
| Ⓜ️ | `M` | Already a tag |
| 🆑 🆒 🆓 🆔 🆕 🆖 🆗 🆘 🆚 | `CL`, `COOL`, `FREE`, `ID`, `NEW`, `NG`, `OK`, `SOS`, `VS` | Already words |
| 🆙 | `UP!` | Already a tag, erased |
| 💯 | `100` | Already a tag |
| 🔞 | `18` | Already a tag |
| 🔠 🔡 🔢 🔤 | `ABCD`, `abcd`, `1234`, `abc` | They show these; already tags |
| 🈁 🈂️ 🈷️ 🈶 🈯️ 🉐 🈹 🈚️ 🈲 🉑 🈸 🈴 🈳 ㊗️ ㊙️ 🈺 🈵 | `ココ`, `サ`, `月`, `有`, `指`, `得`, `割`, `無`, `禁`, `可`, `申`, `合`, `空`, `祝`, `秘`, `営`, `満` | Japanese buttons: their Unicode compatibility form (NFKC), written in the list |

- Unicode's **compatibility form** (NFKC) would give only part of it — `!!`, `!?`, `TM`, `i`, `M` and
  the Japanese ideographs; ❓, ➕, the keycaps and the letter buttons have none: hence the list.
- Matched **case-insensitively**, like every keyword: `ok` finds 🆗, `a` finds 🅰️.

---

## Matching

The typed text is split **at spaces** into typed words, as today. Each typed word is one of two kinds:

| Typed word | Matched against | Example |
|---|---|---|
| **Plain** — letters and digits only | The keyword words, as today | `chat`, `1`, `ok` |
| **With symbols** — at least one other character | The emoji's **symbol keywords** (below); failing that, its letter-and-digit parts as today (`d'or` → `d`, `or`) | `?`, `:)`, `up!`, `1:00` |

- **Symbol keywords**: the words, split at spaces, of an emoji's names and tags in both languages that
  hold a symbol (`?`, `+`, `up!`, `n°1`, `keycap:`), its **emoticons**, and its **characters**.
  Normalized like the keywords — lower case, diacritics removed — but the symbols kept; the
  typographic apostrophe `’` read as `'`.
- A typed word with symbols matches as a plain one does: **contained** in a symbol keyword, graded
  whole / start / inside, then coverage. The fallback keeps every query that works today working:
  `aujourd'hui` still finds what it finds now.
- Side effect, wanted: Emojibase's symbol tags erased today become searchable — `%` and `&` find 🔣,
  `÷` ➗, `✓` ✅ ✔️.

### Ranking

- **New tier `Character`**, above `Exact`: the typed word **equals** one of the emoji's characters, or
  one of its **emoticons** (Q&A 7) — `:)` → 🙂 first, `<3` → ❤️ first. `1` → 1️⃣ first, then the emojis with the whole word
  `1` (🕐…), as today.
- Several typed words: the worst tier still decides (`? rouge` → ❓, its `?` a `Character`, `rouge` an
  `Exact`: tier `Exact`).

---

## Details Panel

- The characters are **added to the tags** shown, so the panel shows what the search finds: 🟰 gains
  `=`, 💲 `$`, #️⃣ `#`. A character the row already holds as a tag is not repeated (❓ already has
  `?`). **Both rows** (Q&A 8), at the end of the tags, each row skipping a character it already
  holds — the English row too when the French one is hidden.
- Added at the catalog's building, to `EnglishTags` / `FrenchTags`: the panel's height
  (`EmojiDetailsPanel.HeightFor`, over the whole catalog) counts them with no change.
- **Highlight**: `EmojiSearch.MatchSpans` highlights a typed word with symbols too — today it
  normalizes the symbols away, so `?` would highlight nothing.
- **Emoticons highlighted too** (Q&A 9): the emoticons under the emoji get the same highlight, in
  `highlightColor`, when the search matches them — `:)` under 🙂.

---

## Test Impact

None: the app has **no test project**, and none is created (Q&A 10). The search is checked on the
built app by calling `EmojiCatalog` and `EmojiSearch` **by reflection on the dll**, like the previous
workfiles. The checks:

| Behaviour to check | Through |
|---|---|
| `?` finds ❓ and ❔ first, then ⁉️ | `EmojiSearch.Find` |
| `1` finds 1️⃣ first, before 🕐 | `EmojiSearch.Find` |
| `ok` finds 🆗 before 👌 | `EmojiSearch.Find` |
| `:)` finds 🙂 first; `<3` ❤️ | `EmojiSearch.Find` |
| `=` finds 🟰, `$` 💲 (characters missing from Emojibase) | `EmojiSearch.Find` |
| `aujourd'hui`, `d'or` find what they found before (fallback) | `EmojiSearch.Find`, against the current build |
| `?` highlighted in ❓'s tags, `=` in 🟰's, `:)` in 🙂's emoticon | `EmojiSearch.MatchSpans` |
| 🟰's tags hold `=` in both rows; ❓'s hold `?` once per row | `EmojiCatalog.Build` |

---

## Open Questions

- [x] ~~**Source of the characters**: one list written by hand, or rules (the keycap's base character,
  NFKC) plus a hand list for the rest?~~ → One list written by hand, every emoji in it
- [x] ~~**The list itself**: keep, drop or add — especially 💯 `100`, 🔞 `18`, 🔠🔡🔢🔤, the Japanese
  ideographs, ✖️ also by `x`.~~ → All kept, ✖️ by `x` too
- [x] ~~**Emoticons in the `Character` tier**: `:)` typed exactly ranks 🙂 first, or as a whole word
  only?~~ → `Character` tier, first
- [x] ~~**Details panel row**: the characters added to both rows' tags, or to the English row
  only?~~ → Both rows, at the end, skipped where the row already has them
- [x] ~~**Emoticon highlight**: highlight the emoticon under the emoji when the search matches it, like
  the names and tags — or not?~~ → Highlighted
- [x] ~~**Tests**: checked on the built dll by reflection, no test project — or create a test project
  for `EmojiSearch`?~~ → By reflection, no test project

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-09

Initial design, from the user's request (*some emojis show a number or a character, ? ❓ or 1 1️⃣: they
should be found by that character*) and the step 5 scoping: four families (keycaps, punctuation and
math, letters, emoticons), ranked first, shown as tags in the details panel.

Exploration (one pass, by the agent — the questions chained around `EmojiSearch`): `Normalize`
erases every symbol, typed or in a keyword; Emojibase already gives many of these characters as
tags (`?`, `+`, `÷`, `✓`) and the keycaps their digit in their name; `=` (🟰), `$` (💲), `#` and `*`
are missing; NFKC covers only a few of them. Design: an emoji's *characters*, a symbol-keyword path
for typed words holding a symbol, with today's matching as fallback, and a `Character` tier above
`Exact`.

### Iteration 2 — 2026-10-09

Q&A 5–8 answered: the characters come from **one hand-written list** (no NFKC, no keycap rule); every
uncertain row kept — 💯 `100`, 🔞 `18`, 🔠🔡🔢🔤, the 17 Japanese buttons, ✖️ by `x` as well as `×`;
an emoticon typed exactly is in the `Character` tier; the characters are added to **both** rows of
the details panel. Still open: the emoticon highlight, the tests.

### Iteration 3 — 2026-10-09

Q&A 9–10 answered: the emoticons under the emoji are highlighted like the names and tags; no test
project — the search is checked by reflection on the built dll. No question left open.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Does not apply: no test project — see *Test Impact* |
| README | | | `README.md` and `README.fr.md` § Search box |
| RULES.md | | | § Search Box |
| GLOSSARY | | | A *Character* term, if kept — `GLOSSARY.md` and `GLOSSARY.fr.md` |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which families must be found by their character? | All four: keycaps, punctuation and math, letters, emoticons | 2026-10-09 |
| 2 | When the typed text is exactly an emoji's character, how is it ranked? | First — a new tier above the whole word | 2026-10-09 |
| 3 | Must the character appear in the details panel? | Yes, as a tag | 2026-10-09 |
| 4 | Is the subject straightforward, or tricky / long? | Straightforward — one exploration pass | 2026-10-09 |
| 5 | Source of the characters: one hand-written list, or rules (keycap base, NFKC) plus a list? | One hand-written list | 2026-10-09 |
| 6 | Which of the uncertain rows of the proposed list are kept? | All: 💯 `100`, 🔞 `18`, 🔠🔡🔢🔤, the Japanese ideographs, ✖️ by `x` too | 2026-10-09 |
| 7 | An emoticon typed exactly: `Character` tier, or whole word only? | `Character` tier — first | 2026-10-09 |
| 8 | Details panel: the characters added to both rows, or the English one only? | Both rows | 2026-10-09 |
| 9 | Highlight the emoticon under the emoji when the search matches it? | Yes | 2026-10-09 |
| 10 | Tests: by reflection on the built dll, or a new test project? | By reflection, no test project | 2026-10-09 |

---

*Last updated: 2026-10-09*
