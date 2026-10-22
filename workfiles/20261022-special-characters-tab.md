# Special Characters Tab

> Working document — a new tab offering useful special characters (©, ®, ™…), read from a file
> deployed with the app that the user can edit.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The goal is to insert easily the characters that are hard to type on a keyboard — legal marks
(© ® ™), typography (« » — …), currencies, maths symbols, arrows… — the same way as an emoji.

- A **new tab** in the tab strip, holding these characters.
- The **list of characters** lives in a **file deployed with the app**, which the user may edit to
  add or remove characters.

The first step, before any technical design, is to settle **which characters** are useful: the
design starts from **families** of characters, refined progressively with the user.

---

## Character Families

**One tab, one section per family**, in this order (Iteration 2). The characters are still a
proposal, to be refined.

| # | Family | Characters |
|---|---|---|
| 1 | **Legal & references** | © ® ™ ℠ ℗ § ¶ † ‡ № |
| 2 | **Punctuation & typography** | « » ‹ › “ ” ‘ ’ „ — – ‑ … • · ¡ ¿ ′ ″ |
| 3 | **Accented letters & ligatures** — the capitals hard to type on AZERTY | É È Ê À Ç Ù Œ œ Æ æ ß Ñ ñ |
| 4 | **Currencies** | € £ ¥ ¢ $ ₹ ₽ ₩ ₺ ₿ ¤ |
| 5 | **Maths, units & fractions** | ± × ÷ ≈ ≠ ≤ ≥ ∞ √ ∑ ∫ ∂ ∆ ° ‰ µ ½ ¼ ¾ ⅓ ² ³ ℃ |

Left out (Iteration 2): **spaces & invisible characters**, **arrows**, **bullets, check marks &
suits**, and the further candidates (Greek letters, superscripts & subscripts, keyboard keys) —
the user can still add them to the file.

---

## Test Impact

To be filled once the design settles (file parsing, the tab's sections, the insertion of the
text form).

| Behaviour to pin | Test file | Create / Update |
|---|---|---|

---

## Open Questions

- [x] ~~Which families are retained?~~ → Legal & references, punctuation & typography, accented letters & ligatures, currencies, maths/units/fractions
- [ ] Which characters within each retained family?
- [x] ~~Structure: one tab with one section per family, or one tab per family?~~ → One tab, one section per family
- [ ] Where does the tab sit in the strip (after the custom tab, last…), and with which icon?
- [ ] The file: format (JSON?), location (next to the exe, like `emoji-data\`?), and what happens
  when the exe ships a newer default list while the user edited theirs (kept, merged, replaced)?
- [ ] Does each character carry a **name** (English / French) — shown in the details panel, and
  searchable from the search box?
- [ ] Do the characters take part in the rest of the app: frequent tab counters, custom groups,
  `Use as tray icon`?
- [x] ~~Invisible characters (spaces, soft hyphen): how are they shown in the grid?~~ → Moot: the
  family is not retained
- [ ] © ® ™ also exist as emojis (©️ ®️ ™️, with `FE0F`): the tab inserts the **text form** — the
  bare character, or with `FE0E` to force the text presentation?
- [ ] Rendering: drawn monochrome with a text font (Segoe UI), or through the emoji renderer?
- [ ] Can the tab be hidden from the settings menu, like the frequent tab?
- [ ] `Reset all settings…`: does it delete the user's edited file (back to the default list)?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-22

Initial request: a new tab with useful special characters (©, ®, ™…), read from a file deployed
with the app and editable by the user. The user asked first for **proposals of character
families**, to be refined progressively: eight families proposed (see *Character Families*),
plus three further candidates. Every technical question is listed under *Open Questions*.

No match in `TODO-FEATURES.md`.

### Iteration 2 — 2026-10-22

Scoping batch answered:

- **Families retained**: legal & references, punctuation & typography, accented letters &
  ligatures, currencies, maths/units/fractions. Left out: spaces & invisible characters, arrows,
  bullets/check marks/suits, and the further candidates.
- **Structure**: one tab, one section per family — like the custom tab and its groups.
- **Depth**: straightforward — a single scout pass of the codebase.

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
| 1 | Which text families are retained (legal, punctuation, accented letters, spaces)? | Legal & references, punctuation & typography, accented letters | 2026-10-22 |
| 2 | Which symbol families are retained (currencies, maths, arrows, bullets)? | Currencies, maths/units/fractions | 2026-10-22 |
| 3 | Structure: one tab with one section per family, or one tab per family? | One tab, one section per family | 2026-10-22 |
| 4 | Depth: straightforward, or tricky / long? | Straightforward |
| 5 | Do the characters take part in the frequent tab, the custom groups, `Use as tray icon`? | | 2026-10-22 |
| 6 | Does each character carry a name (EN / FR), shown in the details panel and searchable? | | 2026-10-22 |
| 7 | Where does the tab sit in the strip? | | 2026-10-22 |
| 8 | The user edited the file and a new exe ships a newer default list: what happens? | | 2026-10-22 | 2026-10-22 |

---

*Last updated: 2026-10-22*
