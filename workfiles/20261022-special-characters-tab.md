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

**One tab, one section per family**, in this order (Iteration 2). This list is the **default
shipped** in the exe (Iteration 7); the user refines it in `special-characters.json`.

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

## Names and Aliases

Each character carries an **English and a French name** — shown in the details panel like an
emoji's, searchable (Iteration 4) — and **aliases**: short, easy-to-type texts describing it, found by the **search
box** (Iteration 3). `<<` finds «; `"` finds every character drawn as quotes — « » “ ” „ ″. They
live in the characters' file, next to each character, so the user edits them too.

- They fit the existing **symbol** search (RULES.md § Search Box): a typed word holding a symbol is
  looked for in the symbol keywords — the aliases join them, like the emojis' **characters**
  (`Data/EmojiCharacters.cs`), and an alias typed whole ranks first.

The default aliases (Iteration 7). The **names** are written by the implementation: the English
one from the Unicode name in lower case (`copyright sign`), the French one the usual French name
(`symbole copyright`, `guillemet ouvrant`).

| Family | Character → aliases |
|---|---|
| Legal & references | © `(c)` `c` · ® `(r)` `r` · ™ `tm` `(tm)` · ℠ `sm` · ℗ `(p)` · § `s` `par` · ¶ `p` `pilcrow` · † `+` · ‡ `++` · № `no` |
| Punctuation & typography | « `<<` `"` · » `>>` `"` · ‹ `<` `'` · › `>` `'` · “ `"` · ” `"` · ‘ `'` · ’ `'` · „ `,,` `"` · — `---` · – `--` · ‑ `-` · … `...` · • `*` · · `.` · ¡ `!` · ¿ `?` · ′ `'` · ″ `"` `''` |
| Accented letters & ligatures | É `e'` `E'` · È `e`` · Ê `e^` · À `a`` · Ç `c,` · Ù `u`` · Œ `oe` · œ `oe` · Æ `ae` · æ `ae` · ß `ss` · Ñ `n~` · ñ `n~` |
| Currencies | € `e` `eur` `euro` · £ `l` `gbp` `pound` · ¥ `y` `yen` · ¢ `c` `cent` · $ `usd` `dollar` · ₹ `inr` `rupee` · ₽ `rub` `ruble` · ₩ `krw` `won` · ₺ `try` `lira` · ₿ `btc` `bitcoin` · ¤ `currency` |
| Maths, units & fractions | ± `+-` · × `x` `*` · ÷ `/` `:` · ≈ `~=` `~~` · ≠ `!=` `<>` `=/` · ≤ `<=` · ≥ `>=` · ∞ `oo` `inf` · √ `sqrt` · ∑ `sum` · ∫ `int` · ∂ `d` · ∆ `delta` · ° `deg` `o` · ‰ `%o` · µ `u` `micro` · ½ `1/2` · ¼ `1/4` · ¾ `3/4` · ⅓ `1/3` · ² `^2` · ³ `^3` · ℃ `°c` |

---

## The File

From the scout pass (Iteration 5) — a proposal, subject to the user's reading.

- **`special-characters.json`, next to the exe** (`SpecialCharacters.FileName`), like
  `custom-groups.json`; its default is **embedded** in the exe (`Data/SpecialCharacters.json`, an
  `EmbeddedResource` with an explicit `LogicalName`), the way `Data/EmojiDataFolder.cs` carries the
  Emojibase copy.
- **Format** — camelCase JSON, UTF-8 without BOM, the characters written as themselves:

  ```json
  {
    "families": [
      {
        "name": "Legal & references",
        "characters": [
          { "character": "©", "name": "copyright sign", "frenchName": "symbole copyright", "aliases": ["(c)", "c"] }
        ]
      }
    ]
  }
  ```

  - `name` of a family: the section's header (English, like the catalog's sections).
  - `character`: any text — usually one character, a sequence allowed.
  - The code point shown in the copy button's tooltip (`U+00A9`) is computed from the text, not
    written in the file.
- **At launch** (`SpecialCharacters.Load`, modelled on `EmojiDataFolder.Load`):

  | The file | Does |
  |---|---|
  | Missing | Written from the embedded default (`.tmp` then a replace), then used |
  | Present and valid | Used as it is — **never rewritten**, even when the exe ships a newer default |
  | Present, invalid — not JSON, or no `families` list | **Kept as it is**, never rewritten: the embedded default used in memory, and a **warning** on the window's first show (see below) |
  | Cannot be written | The embedded default used in memory — never an error |

- **The warning** (Iteration 6): `special-characters.json is not valid: <reason>` — the
  `JsonException`'s message, its line included — `The default list is used until it is fixed.`, a
  warning icon, OK. Shown **once**, at the window's **first show** (`OnShown`, or the first Win+; of
  a `--background` launch) — never a dialog before any window exists.
- **Leniency inside a valid file**, like `custom-groups.json`: a character without `character` is
  left out; a character given twice keeps its first entry; missing names or aliases are empty; a
  family without a character is not shown.
- **`Reset all settings…`** deletes it (and its `.tmp`) — a row in `Data/AppReset.cs`, a line in
  the confirmation's list: the default list comes back at the restart.

---

## Tab and Sections

- **Tab icon**: Segoe Fluent Icons `E8C1` (*Characters*), `MainForm.SpecialIcon`, next to
  `FrequentIcon` and `CustomIcon` (Iteration 6).
- **One tab, last** in the strip, after the seven catalog tabs; **one section per family**, in the
  file's order — the custom tab's mechanism (`MainForm.TabOf` / `SectionOf`), with a trailing block
  of sections and one tab. The minimum window width grows by one tab.
- The sections are `EmojiCategory` objects built from the file (`Data/SpecialCharacters.cs`), each
  character an `Emoji` (`Text`, `Name`, `FrenchName`, keywords) — kept **apart** from the catalog's
  categories (`MainForm.specialCategories`).
- **Insertion**: click, Enter, Ctrl+click, Ctrl+Enter insert the **bare character**, as written in
  the file — no `FE0E`: the catalog's ©️ ®️ ™️ carry `FE0F`, so the special © is another text, and
  a target app draws a bare © as text.
- **Not counted** by the frequent tab (`MainForm.OnEmojiUsed` returns early for a special
  character); **no `Use as tray icon`** in its right-click menu (nor the separator after it).
- **Hidden** (Iteration 6): `Show special characters`, a checked item of the settings menu, in a
  section of its own — *special characters* — right after the *custom groups* section (the window's
  order: the special tab comes after the custom one). No confirmation; saved as `showSpecial` in
  `settings.json` (`SettingsFile.ReadShowSpecial`; missing or unreadable → shown). Hidden, the tab
  leaves the strip, its sections the grid **and the search results**; a toggle goes through
  `MainForm.RebuildSections`, like the frequent tab's. A file with no character shows no tab either.
- **Custom groups**: `Add to ▸` works as for an emoji — `MainForm.emojisByText` includes the special
  characters, or a group would drop them silently.

---

## Rendering

- The grid and the details panel draw through `Drawing/EmojiRenderer.cs`, whose only font is
  Segoe UI Emoji: a bare © ® ™ would come out **in colour**, and ₹ ∑ through DirectWrite's fallback,
  with uneven metrics.
- Decided (Iteration 6): the special characters are drawn **monochrome with Segoe UI**, the text font — a second
  `IDWriteTextFormat` in the renderer, chosen for them, at a smaller scale than the emojis' so a
  letter does not fill its cell. Colour: the text colour.
- **Pre-render**: the special characters join the grid's pre-render list (the cache key holds the
  list: rendered again automatically); the renderer's change **bumps
  `EmojiBitmapCache.FormatVersion`** to 2.

---

## Search and Details Panel

- **Search**: `EmojiSearch.Find` runs over the catalog's categories **and** the special ones (one
  call in `MainForm`); `EmojiSearch` itself does not change. Per character:
  - `Keywords` — the words of its English and French names;
  - `SymbolKeywords` — its aliases holding a symbol (`<<`, `"`, `(c)`), folded by `EmojiSearch.Fold`;
  - `CharacterKeys` — every alias, folded: an alias typed whole ranks first, like an emoji's
    character (`1` → 1️⃣).
- **Details panel**: the character drawn large, its English and French names, its **aliases as
  tags** in both rows; the copy button copies the bare character. The panel's height takes the
  special characters into account (its catalog list includes them).

---

## Test Impact

**No unit test**: the repository has no test project. The file's loading and parsing
(`SpecialCharacters.Load` / `Parse`) are checked **by reflection on the dll of a copy of the build
folder** (`APP_CONTEXT_BASE_DIRECTORY`, RULES.md § Reset All Settings): missing file written,
valid edited file kept, invalid file handled as decided. The tab, the rendering and the search are
checked by hand (custom-drawn, no accessibility), the search also by `WM_SETTEXT` into the box.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project; reflection checks and by hand) | — | — |

---

## Open Questions

- [x] ~~Which families are retained?~~ → Legal & references, punctuation & typography, accented letters & ligatures, currencies, maths/units/fractions
- [ ] Which characters within each retained family?
- [x] ~~Structure: one tab with one section per family, or one tab per family?~~ → One tab, one section per family
- [x] ~~Where does the tab sit in the strip?~~ → **Last**, after the seven emoji categories
- [x] ~~Which icon for the tab?~~ → Segoe Fluent Icons `E8C1`, *Characters*
- [x] ~~The exe ships a newer default list while the user edited theirs?~~ → **The user's file
  wins**: never rewritten while it exists and is valid; `Reset all settings…` brings the default back
- [x] ~~The file: format, name and location?~~ → `special-characters.json` next to the exe, JSON,
  default embedded (see *The File*)
- [x] ~~An **invalid** file: rewritten from the default, or kept?~~ → Kept as it is, the default used
  in memory, and a warning on the window's first show
- [x] ~~Is a character searchable from the search box?~~ → Yes, by its **aliases** (see *Names and Aliases*)
- [ ] Which aliases for each character?
- [x] ~~Does each character also carry a **name** (English / French)?~~ → Yes: shown in the details
  panel like an emoji's, and searchable, on top of the aliases
- [x] ~~Do the characters take part in the rest of the app?~~ → **Custom groups only** (`Add to ▸`);
  no frequent counters, no `Use as tray icon`
- [x] ~~Invisible characters (spaces, soft hyphen): how are they shown in the grid?~~ → Moot: the
  family is not retained
- [x] ~~© ® ™ also exist as emojis: bare character or `FE0E`?~~ → The bare character: the catalog's
  ones carry `FE0F`, so they are other texts (see *Tab and Sections*)
- [x] ~~Rendering: monochrome text font, or through the emoji renderer?~~ → Monochrome, Segoe UI,
  the text colour, smaller than the emojis
- [x] ~~Can the tab be hidden from the settings menu?~~ → Yes, `Show special characters`, saved as
  `showSpecial`
- [x] ~~Is the character and alias list the default to ship?~~ → Yes, as it stands
- [x] ~~A settings menu item opening `special-characters.json` in the user's editor?~~ → No: `Open app
  folder` is enough; an edit is read at the next launch
- [x] ~~`Reset all settings…`: does it delete the user's edited file?~~ → Yes, back to the default
  list (Iteration 4)

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

### Iteration 3 — 2026-10-22

User request: the characters need **aliases**, easy to use, describing them — `<<` for the left
double angle quote, and `"` matching it too. A first alias list is proposed (see *Names and Aliases*), found
through the search box's symbol search.

### Iteration 4 — 2026-10-22

Second scoping batch answered:

- **Integration**: the characters can be added to the **custom groups**; they are **not** counted
  by the frequent tab, and `Use as tray icon` is not offered on them.
- **Names**: an English and a French name per character, shown in the details panel like an
  emoji's and searchable, **on top of** the aliases.
- **Tab position**: last, after the seven emoji categories.
- **Default list update**: the user's file wins — never rewritten while it exists and is valid;
  `Reset all settings…` deletes it, so the default list comes back.

### Iteration 5 — 2026-10-22

Scout pass of the codebase (three read-only agents: category wiring, rendering, deployed file).
Technical design written: *The File*, *Tab and Sections*, *Rendering*, *Search and Details Panel*,
*Test Impact*. Findings that shaped it:

- A special character can be an `Emoji`; the frequent and custom tabs already show non-catalog
  sections, and the custom tab's one-tab-several-sections mapping is reused.
- `MainForm.emojisByText` is built from the catalog only: custom groups would drop the special
  characters silently — it must include them.
- The catalog's © ® ™ are `©️ ®️ ™️` (`FE0F`): no clash with the bare characters.
- The renderer knows Segoe UI Emoji only: a bare © would be drawn in colour — hence a text format.
- No existing file follows "the user's edits win": `EmojiDataFolder` rewrites an older folder; the
  new file drops that comparison.

### Iteration 6 — 2026-10-22

Third batch answered:

- **Tab icon**: `E8C1`, *Characters*.
- **Rendering**: monochrome, Segoe UI, the text colour, smaller than the emojis.
- **Invalid file**: kept as it is, the default used in memory, plus a warning on the window's first
  show.
- **Hidden**: `Show special characters` in the settings menu, a section of its own after the custom
  groups', saved as `showSpecial`.

Two questions added: the default list to ship, and a menu item opening the file.

### Iteration 7 — 2026-10-22

- **Default list**: the families, characters and aliases as they stand are the default shipped.
- **No menu item** opening the file: `Open app folder` finds it; an edit is read at the next launch.

No open question left.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| RULES.md | | | |
| README | | | |
| CONTRIBUTING.md | | | |
| GLOSSARY | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which text families are retained (legal, punctuation, accented letters, spaces)? | Legal & references, punctuation & typography, accented letters | 2026-10-22 |
| 2 | Which symbol families are retained (currencies, maths, arrows, bullets)? | Currencies, maths/units/fractions | 2026-10-22 |
| 3 | Structure: one tab with one section per family, or one tab per family? | One tab, one section per family | 2026-10-22 |
| 4 | Depth: straightforward, or tricky / long? | Straightforward |
| 5 | Do the characters take part in the frequent tab, the custom groups, `Use as tray icon`? | Custom groups only | 2026-10-22 |
| 6 | Besides the aliases, does each character carry a name (EN / FR), shown in the details panel and searchable? | Yes, names EN / FR + aliases | 2026-10-22 |
| 7 | Where does the tab sit in the strip? | Last | 2026-10-22 |
| 8 | The user edited the file and a new exe ships a newer default list: what happens? | The user's file wins; Reset all settings brings the default back |
| 9 | Which icon for the tab? | E8C1, Characters | 2026-10-22 |
| 10 | Rendering: monochrome text font, or like the emojis? | Monochrome text font | 2026-10-22 |
| 11 | An invalid file after an edit: rewritten from the default, or kept and the default used in memory? | Kept, default in memory, plus a warning | 2026-10-22 |
| 12 | Can the tab be hidden from the settings menu? | Yes, `Show special characters` | 2026-10-22 |
| 13 | Is the character and alias list the default to ship? | Yes, as it stands | 2026-10-22 |
| 14 | A settings menu item opening the file in the user's editor? | No | 2026-10-22 | 2026-10-22 | 2026-10-22 |

---

*Last updated: 2026-10-22*
