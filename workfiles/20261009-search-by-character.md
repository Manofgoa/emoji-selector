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

### Proposed list

Source to settle (see *Open Questions*). Draft, to be reviewed:

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
| ✖️ | `×` | Already a tag, erased |
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
| 💯 | `100` | To confirm |
| 🔞 | `18` | To confirm |
| 🔠 🔡 🔢 🔤 | `ABCD`, `abcd`, `1234`, `abc` | To confirm — they show these |
| 🈁 🈂️ 🈷️ 🈶 🈯️ 🉐 🈹 🈚️ 🈲 🉑 🈸 🈴 🈳 ㊗️ ㊙️ 🈺 🈵 | `ココ`, `サ`, `月`, `有`, `指`, `得`, `割`, `無`, `禁`, `可`, `申`, `合`, `空`, `祝`, `秘`, `営`, `満` | To confirm — Japanese buttons, their Unicode compatibility form (NFKC) |

- Unicode's **compatibility form** (NFKC) gives only part of it: `!!`, `!?`, `TM`, `i`, `M` and the
  Japanese ideographs. ❓, ➕, the keycaps and the letter buttons have none — a list written by hand
  is needed either way.
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
  one of its emoticons (see *Open Questions*). `1` → 1️⃣ first, then the emojis with the whole word
  `1` (🕐…), as today.
- Several typed words: the worst tier still decides (`? rouge` → ❓, its `?` a `Character`, `rouge` an
  `Exact`: tier `Exact`).

---

## Details Panel

- The characters are **added to the tags** shown, so the panel shows what the search finds: 🟰 gains
  `=`, 💲 `$`, #️⃣ `#`. A character the row already holds as a tag is not repeated (❓ already has
  `?`). Which row(s): see *Open Questions*.
- Added at the catalog's building, to `EnglishTags` / `FrenchTags`: the panel's height
  (`EmojiDetailsPanel.HeightFor`, over the whole catalog) counts them with no change.
- **Highlight**: `EmojiSearch.MatchSpans` highlights a typed word with symbols too — today it
  normalizes the symbols away, so `?` would highlight nothing.

---

## Test Impact

To settle (see *Open Questions*): the app has **no test project**; the previous workfiles checked the
search on the built app, by reflection on the dll. The behaviours an assertion must prove either way:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| `?` finds ❓ and ❔ first, then ⁉️ | — | — |
| `1` finds 1️⃣ first, before 🕐 | — | — |
| `ok` finds 🆗 before 👌 | — | — |
| `:)` finds 🙂 first; `<3` ❤️ | — | — |
| `=` finds 🟰, `$` 💲 (characters missing from Emojibase) | — | — |
| `aujourd'hui`, `d'or` find what they found before (fallback) | — | — |
| `MatchSpans` highlights `?` in ❓'s tags, `=` in 🟰's | — | — |
| 🟰's tags hold `=`; ❓'s hold `?` once | — | — |

---

## Open Questions

- [ ] **Source of the characters**: one list written by hand, every emoji in it (recommended:
  explicit, one place) — or rules (the keycap's base character, NFKC) plus a hand list for the rest?
- [ ] **The list itself** (*Characters* § Proposed list): keep, drop or add — especially 💯 `100`,
  🔞 `18`, 🔠🔡🔢🔤, the Japanese ideographs, ✖️ also by `x`.
- [ ] **Emoticons in the `Character` tier**: `:)` typed exactly ranks 🙂 first (recommended) — or as
  a whole word only?
- [ ] **Details panel row**: the characters added to both rows' tags, at the end, skipped where the
  row already has them (recommended) — or to the English row only?
- [ ] **Emoticon highlight**: highlight the emoticon under the emoji when the search matches it, like
  the names and tags (recommended) — or not?
- [ ] **Tests**: checked on the built dll by reflection, no test project, like the previous workfiles
  (recommended) — or create a test project for `EmojiSearch`?

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

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
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

---

*Last updated: 2026-10-09*
