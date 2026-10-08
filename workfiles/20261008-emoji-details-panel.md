# Emoji Details Panel

> Working document — a panel at the bottom of the window showing the selected emoji large, its
> English and French names and tags, its emoticon, and a button copying it to the clipboard.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

A **details panel** docked at the bottom of the main window, below the grid, inspired by Twitter's
emoji picker (the hovered emoji shown large at the bottom left). It shows the grid's **selection**
— the mouse moving over an emoji selects it, the arrows move it — so "hovered or selected" is one
and the same emoji: `EmojiGrid.SelectedEmoji`.

What it shows, from the embedded Emojibase data (`compact.en.json` / `compact.fr.json`):

| Shown | Source | Example (😂) |
|---|---|---|
| The emoji, large and in colour | `unicode` | 😂 |
| The English name, after a US flag | en `label`, capitalized | Face with tears of joy |
| The English tags, **all of them** | en `tags` | crying, face, feels, funny, haha, happy, hehe, hilarious, joy, laugh, lmao, lol, rofl, roflmao, tear |
| The French name, after a French flag — when the French row is on | fr `label`, capitalized | Visage riant aux larmes |
| The French tags, all of them — same condition | fr `tags` | content, heureux, joie, larmes, lol, mdr, pleurer de joie, pleurer de rire, rire aux larmes, sourire, émoticône |
| The emoticon, when there is one (49 emojis) | `emoticon` (a string or an array) | `:')` |
| The first code point, on a button copying the emoji | `hexcode` | `U+1F602` (👨‍👩‍👧‍👦 → `U+1F468`) |

Left out for now: `group` (the category) and `skins` (skin tone variants). No colour choice (skin
tone) either, unlike Twitter.

Out of scope: the search itself — English and French names and tags are already searched since
[20261007-search-box.md](20261007-search-box.md).

---

## Layout

**One row per language**, as in the mockup of 2026-10-08 (design B first, in two columns, then
turned into rows — see Iterations 1 and 3):

```
┌──────────────────────────────────────────────────────────────┐
│ ┌────┐  🇺🇸 Face with tears of joy                [⧉ U+1F602] │
│ │ 😂 │  crying, face, feels, funny, haha, happy, hehe,         │
│ └────┘  hi[la]rious, joy, [la]ugh, lmao, lol, rofl, roflmao,   │
│  :')    tear                                                  │
│         ─────────────────────────────────────                 │
│         🇫🇷 Visage riant aux [la]rmes                           │
│         content, heureux, joie, [la]rmes, lol, mdr, pleurer   │
│         de joie, pleurer de rire, rire aux [la]rmes, sourire, │
│         émoticône                                             │
└──────────────────────────────────────────────────────────────┘
```

- **Left**: the emoji, large (≈ 48 logical px), drawn in colour; the emoticon(s) under it, small and
  grey, only when the emoji has one (an array shows every one, space-separated).
- **Middle**: one row per language, stacked, each one the whole middle width, a thin separator
  between them.
  - **EN row**, always shown: the US flag then the English name (bold), the English tags below.
  - **FR row**, shown when the French row is on (default): the French flag then the French
    name (bold, first letter capitalized — `Visage`, not `visage`), the French tags below.
  - French row off → only the EN row; the panel is lower (see *Height*).
  - The flags are **images embedded in the exe** (US and French, two sizes for the DPI) — Segoe UI
    Emoji has no flag glyphs.
  - Tags are joined with `, ` and **wrap** to as many lines as they need — never truncated, no `…`.
- **Right**: a **button** reading the emoji's **first code point only** (`U+1F602`; 👨‍👩‍👧‍👦 →
  `U+1F468`), with a copy glyph. A click copies **the emoji itself** (its whole Unicode character
  sequence, `😂`) to the clipboard as text.
  - After a copy the button reads `Copied` for about a second, then its code point again.
  - A copy neither hides the window nor counts as a use (the frequent tab ignores it).
- **No selection** (a search with no result): the panel stays, empty, at the same height.

The grid's **tooltip** (the hovered emoji's name) is **removed**: the panel shows that name.

### Height

**Fixed, sized to the longest content**: the panel height is the one the emoji with the most text
needs at the current panel width (and French row state), computed over the whole catalog. Moving
the selection never changes it; resizing the window or toggling the French row recomputes it.

- The window's minimum height and its default size (`MainForm.DefaultClientSize`) include it.

---

## Search Highlight

While the search box holds text, the characters of the names and tags **matched by the search** are
highlighted.

- **Exactly the matched characters**: `rir` highlights `rir` in `rire aux larmes`, not the word nor
  the tag.
- The same matching as the search ([20261007-search-box.md](20261007-search-box.md) § Matching):
  accent- and case-insensitive (`emoticone` highlights `émoticône`), every word of the query matched
  on its own, inside a single word (never across a space, an apostrophe or a hyphen).
- **Every occurrence** is highlighted, in every name and tag of both rows.
- Colour: **fluorescent yellow** by default (`#FFFF00`), the text keeps its colour.

---

## Settings

Two items join the settings menu (`MainForm.CreateSettingsMenu`), persisted in `settings.json`
(`Data/SettingsFile.cs`, the existing read-modify-write pattern — a key appears once changed):

| Item | Kind | Key | Default |
|---|---|---|---|
| `Show French names` | Check item, toggles the FR row | `showFrench` (bool) | on |
| `Highlight color…` | Opens the Windows colour dialog (`ColorDialog`) | `highlightColor` (`#RRGGBB`) | `#FFFF00` |

An unreadable `highlightColor` falls back to the default.

---

## Technical Notes

From the exploration (2026-10-08):

| Point | Where | What changes |
|---|---|---|
| Data kept per emoji | `Data/Emoji.cs`, `Data/EmojiCatalog.cs` (`Entry`, `Load`) | `Emoji` gains `Hexcode`, `FrenchName` (capitalized like `Name`), `EnglishTags`, `FrenchTags` (raw text) and `Emoticons`. `Entry.Emoticon` parsed tolerantly — a string or an array. A missing French entry → empty French name and tags. Only one constructor call; `Emoji` equality is never used |
| Highlight spans on raw text | `Data/EmojiSearch.cs` | New `MatchSpans(raw, query)`: normalizes the raw text char by char (`œ`/`æ` → 2 chars, marks dropped, punctuation → space) keeping a map back to the raw index, finds every occurrence of every query word inside a word, returns merged raw spans |
| Selection change | `UI/EmojiGrid.cs` (`SetSelection`) | New public `SelectedEmojiChanged` event, raised when the selection changes — including to null |
| Panel | new `UI/EmojiDetailsPanel.cs` | Custom-drawn, `Dock.Bottom`, added right after the grid in `MainForm` (docking order). Owns its own `EmojiRenderer` (the grid's cache renders at the grid size only), re-renders on selection change only |
| Window size | `UI/MainForm.cs` (`MinimumSize`, `DefaultClientSize`) | Panel height added to both |
| Search text | `UI/MainForm.cs` (`OnSearchTextChanged`) | Passed to the panel to redraw the highlight |
| Clipboard | — (none today) | `Clipboard.SetText` on the UI thread, `ExternalException` caught |
| Flags | new embedded resources | Two PNGs per flag (US, FR) at 1× and 2×, the size picked by DPI; public-domain flag designs |
| Grid tooltip | `UI/EmojiGrid.cs` (`toolTip`, `SetHovered`) | Removed |
| Frame | `UI/WindowFrame.cs` | No clash: the bottom resize border is Windows' own, outside the client area |

---

## Documentation

| File | Change |
|---|---|
| `README.md` / `README.fr.md` | *Features*: the details panel, the copy button, the two settings |
| `RULES.md` | A *Details Panel* section: what it shows, fixed height, highlight, settings keys, copy button; the grid's tooltip gone |
| `GLOSSARY.md` / `GLOSSARY.fr.md` | *Details panel* (*panneau de détails*), *Highlight* (*surlignage*) |

---

## Test Impact

**Nothing testable changes in a test file**: the app has no test project (see
[20261007-search-box.md](20261007-search-box.md) § Test Impact) — every behaviour is checked by hand
on the running app.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none (checked by hand: panel follows hover and arrows, FR row on/off, tags wrapped and complete, fixed height while moving, `la` / `emoticone` / `oe` highlighted, colour change saved, copy button pastes the emoji) | — | — |

---

## Open Questions

- [x] ~~1. Does the panel include the search in English and French?~~ → No: already delivered by [20261007-search-box.md](20261007-search-box.md)
- [x] ~~2. Hovered or selected emoji?~~ → The selection: hovering selects since [20261007-keyboard-navigation.md](20261007-keyboard-navigation.md)
- [x] ~~3. Which labels?~~ → All the data except `group` and `skins`: EN and FR names, EN and FR tags, emoticon, code points
- [x] ~~4. Which layout?~~ → B (two columns), code point button on the right, US / French flag before each name *(revised 2026-10-08, see Iteration 3: one row per language)*
- [x] ~~5. Long tag lists?~~ → All tags shown, wrapped, never truncated
- [x] ~~6. Panel height?~~ → Fixed, sized to the longest content
- [x] ~~7. What does the copy button copy?~~ → The emoji itself
- [x] ~~8. What is highlighted?~~ → Exactly the matched characters, in fluorescent yellow, colour customizable in the settings menu
- [x] ~~9. French column?~~ → Optional, on by default
- [x] ~~10. How are the flags drawn? Segoe UI Emoji has no flag glyphs (see [TODO-FEATURES.md](TODO-FEATURES.md) *Flags tab*)~~ → Embedded PNG images
- [x] ~~11. What does the button read for a multi-code-point emoji (👨‍👩‍👧‍👦 = `1F468-200D-1F469-200D-1F467-200D-1F466`)?~~ → The first code point only
- [x] ~~12. Does the grid's tooltip (the hovered emoji's name) stay, now that the panel shows the name?~~ → Removed
- [x] ~~13. Details: panel when the grid has no selection (a search with no result); feedback after a copy; does a copy hide the window or count as a use?~~ → Empty panel at the same height; `Copied` for about a second; a copy neither hides the window nor counts as a use

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-08

Initial design, after waiting for [20261007-search-box.md](20261007-search-box.md) to be delivered
(the user's request, Q&A #5) and reading what it brought: the search on English and French names
and tags exists, the grid has a selection that follows the mouse. Four read-only scouts mapped the
settings menu, the catalog data, the search matching and the window layout (see *Technical Notes*).

Mockups of four layouts (compact, two columns, tag pills, labelled lines) → **B** chosen, then
revised: code point button moved to the right, a US flag before the English name, a French flag
before the French name, everything else as listed in *Overview*, *Layout*, *Search Highlight* and
*Settings*.

### Iteration 2 — 2026-10-08

Open questions 10–13 answered (Q&A #15–18): flags as embedded PNG images, the button reads the first
code point only, the grid's tooltip removed, an empty panel when nothing is selected, `Copied`
feedback after a copy, a copy neither hides the window nor counts as a use. *Layout* and *Technical
Notes* updated.

### Iteration 3 — 2026-10-08

The user's request (Q&A #20): no more one column per language — **one row per language**, stacked:
the EN row (flag, name, tags), then the FR row under it when it is on. The flags, the code point
button on the right, the emoticon under the emoji stay. *Layout* rewritten; the "French column"
setting becomes the "French row" (same menu item, `Show French names`).

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project (see *Test Impact*) |
| README | | | |
| RULES | | | |
| Glossary | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Search on EN/FR names and aliases: part of this workfile, or data only? | Dismissed (session interrupted, then dismissed) | 2026-10-07 |
| 2 | With no hovered emoji, what does the panel show? | Dismissed — moot: the selection stays when the mouse leaves | 2026-10-07 |
| 3 | What are "aliases": shortcodes, tags, both? | Dismissed — then answered by Q&A #9 | 2026-10-07 |
| 4 | Is the subject straightforward or tricky / long? | Dismissed — then answered by Q&A #13 | 2026-10-07 |
| 5 | (same batch, asked again) | Dismissed: wait for the *Search box* session to finish, then analyse what it did | 2026-10-08 |
| 6 | Which aliases does the panel show: English tags, EN + FR tags, shortcodes? | Dismissed | 2026-10-08 |
| 7 | Does the grid's tooltip stay once the panel exists? | Dismissed — asked again as Open Question 12 | 2026-10-08 |
| 8 | What labels are available in the embedded data? | (user's question) — Inspired by Twitter: emoji large at the bottom left; no colour choice; list the available fields | 2026-10-08 |
| 9 | (answer to #8: label, tags, emoticon, hexcode, group, skins) | All of them except `group` and `skins`; make layout mockups | 2026-10-08 |
| 10 | Mockups A–D: which layout? | B, every tag shown with line wrapping (no `…`), search matches highlighted in fluorescent yellow (colour customizable in the settings menu), capital `Visage`, French column optional and on by default, `U+1F602` a button copying to the clipboard | 2026-10-08 |
| 11 | What does the `U+1F602` button copy? | The emoji | 2026-10-08 |
| 12 | Panel height with every tag shown? | Fixed, sized to the longest | 2026-10-08 |
| 13 | What is highlighted: the matched part, the word, the whole tag? Simple or tricky subject? | The matched part; simple | 2026-10-08 |
| 14 | (mid-exploration request) | B with the code point on the right, US flag before the English name, French flag before the French name when the French column is on; make the mockup | 2026-10-08 |
| 15 | How are the flags drawn? | Embedded images | 2026-10-08 |
| 16 | Button label for a multi-code-point emoji? | The first code point only | 2026-10-08 |
| 17 | Does the grid's tooltip stay? | Removed | 2026-10-08 |
| 18 | Empty panel, copy feedback, copy hides the window / counts as a use? | OK for all: empty panel, `Copied` ~1 s, no hide, not a use | 2026-10-08 |
| 19 | Mark the backlog row *Copy (UTF-8)* with this workfile? | | |
| 20 | (request on the revised mockup B) | Not bad, but rows per language instead of columns | 2026-10-08 |

---

*Last updated: 2026-10-08*
