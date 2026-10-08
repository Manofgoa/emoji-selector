# Skin Tones

> Working document — show, insert and copy the emojis that have skin-tone variants in the tone the
> user chose: a default tone picked in the details panel, and a tone of its own for any emoji.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today `Data/EmojiCatalog.cs` leaves the skin-tone variants out (RULES.md § Categories and
Insertion): every person, hand or body-part emoji is the yellow one. This workfile adds the five
Unicode skin tones (Fitzpatrick modifiers `1F3FB`–`1F3FF`).

Agreed scope (step 5 scoping, Q&A 1–4):

| Point | Decision |
|---|---|
| How a tone is picked | **Both**: a **default tone** for every emoji that has variants, and a **tone of its own** per emoji, overriding the default |
| Where the default tone is picked | In the **details panel**, at the bottom of the window — not the settings menu, not next to the search box |
| Two-person emojis (🤝, 🧑‍🤝‍🧑, 💏, 👫…) | The details panel shows **two colours**, one per person |

Components: `Data/EmojiCatalog.cs` (reading `skins`), `Data/Emoji.cs` (the variants), a new
`Data/SkinTone.cs` (the tones, resolving an emoji's text), `UI/EmojiDetailsPanel.cs` (the tone bar),
`UI/MainForm.cs` (the right-click menu, insertion, the stores), `UI/EmojiGrid.cs` and
`Drawing/EmojiBitmapCache.cs` (what is drawn and pre-rendered), `Data/SettingsFile.cs`, the per-emoji
tone store, `Data/AppReset.cs`; README / RULES / GLOSSARY.

---

## Emojibase Data

What the embedded data (Emojibase 17.0.0, `compact.en.json` / `compact.fr.json`) carries — explored,
not a choice:

- **330** base emojis have a `skins` array, **2,030** variants in all: 330 × 5 one-tone variants, plus
  **380** mixed ones — about 19 two-person emojis × 20 combinations (5 × 5 minus the 5 uniform ones).
- A variant has only `hexcode`, `label`, `unicode`, `group`, `order`: **no `tone` field, no tags, no
  emoticon**. Its tones are read from its text — the `1F3FB`–`1F3FF` code points it holds, in order.
- Example, 👍 `1F44D`: `👍🏻` `1F44D-1F3FB` *thumbs up: light skin tone* … `👍🏿`. Two-person, 🧑‍🤝‍🧑:
  `1F9D1-1F3FB-200D-1F91D-200D-1F9D1-1F3FC` *people holding hands: light skin tone, medium-light skin
  tone* — every 5 × 5 combination. 🤝's mixed variants are built from other code points
  (`1FAF1-1F3FF-200D-1FAF2-1F3FE`): the text is taken from `unicode` as it is, never composed.
- The variants are **not** at the top level: the swatches `1F3FB`… are, in the components group the
  catalog already leaves out.
- `compact.fr.json` has the same variants, same hexcodes, French labels (*pouce vers le haut : peau
  claire*). Tone names: light / *claire*, medium-light / *moyennement claire*, medium / *mate*,
  medium-dark / *moyennement foncée*, dark / *foncée*.
- Today the variants are dropped because `EmojiCatalog.Entry` does not declare `skins`
  (`EmojiCatalog.cs:147`): System.Text.Json ignores it.

---

## Tones

- **Six choices**, as in Win+;: **Default** (the yellow emoji, no modifier), then **Light**,
  **Medium-light**, **Medium**, **Medium-dark**, **Dark** (`SkinTone`, a new enum).
- **An emoji's tone** = its own tone when it has one, else the **default tone**. Its **shown text** is
  the variant holding that tone (or the base emoji for *Default*) — what the grid draws, what is
  inserted, what the copy button copies.
- **Two-person emojis**: their tone is a **pair** (first person, second person). Unicode has no
  "one yellow, one toned": *Default* applies to both or to neither. How the pair is picked: see
  *Open Questions*.
- An emoji without variants (🍎, 😀) is never affected.

### Default tone

- Picked in the **details panel** (see *Details Panel* below), saved in `settings.json` as
  `skinTone` (`"default"`, `"light"`, `"medium-light"`, `"medium"`, `"medium-dark"`, `"dark"`;
  missing or unknown → `default`).
- A change applies at once to **every section**: categories, frequent, custom groups, search results.

### Tone of its own

- Set per emoji from its **right-click menu** — `Skin tone ▸`, after `Use as tray icon` — only on an
  emoji that has variants: the six tones, plus **`Use default tone`**, checked while the emoji follows
  the default. Reachable from the keyboard too (Menu key / Shift+F10). Shape of the menu for a
  two-person emoji: see *Open Questions*.
- Kept by the **base emoji's text**, in a store whose place is open (see *Open Questions*); an emoji the
  catalog no longer has stays in the file and is ignored, like the counters.

---

## Details Panel

- A **tone bar**: six round swatches — yellow, then the five skin colours — the one in use circled,
  each with its tone name as tooltip. Custom-drawn and hit-tested in `EmojiDetailsPanel`, like the
  copy button; a click raises a new event to `MainForm`, which saves and applies.
- For a **two-person** emoji the panel shows **two** colours — one bar per person (see *Open
  Questions* for what they set).
- What the bar sets (the default tone, or the selected emoji's), when it shows (only on an emoji with
  variants, or always), and where it sits (it costs height: the panel's height is fixed over the whole
  catalog, `HeightFor`): see *Open Questions*.
- The emoji drawn at 48 px is the **shown text**, in its tone; the copy button copies it; its tooltip
  stays the first code point (`U+1F44D`, the tone modifier coming later in the sequence).
- Names and tags: the base emoji's — the variants have no tags. Whether the name says the tone: see
  *Open Questions*.

---

## Stores Keyed by Text

The counters (`usage.json`), the custom groups (`custom-groups.json`) and the tray emoji
(`trayEmoji`) are keyed by the emoji's **exact text**, looked up in `MainForm.emojisByText` — built
from the base emojis' texts. A toned text stored as such would be dropped as "not in the catalog".

Proposal (see *Open Questions*): the counters and the groups keep the **base text** — a use of 👍🏽
counts for 👍, a group holds 👍 — and show it in its current tone. The existing files keep working:
their texts are base texts today.

---

## Search

- `EmojiSearch` matches the base emojis' keywords: **no entry per variant** — 2,030 more would give
  every result six times. The results are shown in their tone like any section.
- The tone names (*medium skin tone*, *peau mate*) are not added as keywords.

---

## Pre-render Cache

- The grid draws only what `EmojiBitmapCache` pre-rendered (a missing text → fluorescent green, no
  on-demand render), and the cache's key hashes the whole emoji list: a different list re-renders
  everything.
- Which variants are pre-rendered — all 2,030 once (a tone change instant, the atlas about twice as
  large), or the tones in use only (a tone change re-renders) — see *Open Questions*.

---

## Reset All Settings

- `skinTone` goes with `settings.json`. A new file for the per-emoji tones gets its row in
  `AppReset` (its `FileName` / `TemporaryFileName` constants), and the confirmation's list names the
  skin tones.

---

## Test Impact

To settle (see *Open Questions*): the app has **no test project**; the previous workfiles checked the
behaviour on the built app, by reflection on the dll. The behaviours an assertion must prove either
way:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| The catalog reads `skins`: 330 emojis with variants, 5 for 👍, 25 for 🧑‍🤝‍🧑, none for 🍎 | — | — |
| A variant's tones are read from its text: 👍🏽 → Medium; 🧑🏻‍🤝‍🧑🏼 → (Light, Medium-light) | — | — |
| Shown text: default tone Medium → 👍🏽; own tone Dark on 👍 → 👍🏿 whatever the default; 🍎 unchanged | — | — |
| Two-person shown text follows the pair (🤝 Dark + Medium-dark → `1FAF1-1F3FF-200D-1FAF2-1F3FE`) | — | — |
| `skinTone` read / written in `settings.json`; missing or unknown → Default | — | — |
| The per-emoji tones store: round trip, keyed by the base text, unknown emoji kept | — | — |
| A use of a toned emoji counts for its base text in `usage.json` | — | — |
| The search returns base emojis once each, no variant entry | — | — |
| `AppReset` deletes the per-emoji tones file | — | — |

---

## Open Questions

- [ ] **What the details panel's tone bar sets**: the **default tone**, the bar shown only while the
  selection has variants (recommended) — or the default tone, the bar always shown — or the
  **selected emoji's own** tone?
- [ ] **Two-person emojis in the panel**: the second bar is a **second default tone**, for the second
  person (`secondSkinTone`, following the first until changed) (recommended reading of Q&A 3) — or
  the two bars set **that emoji's own pair**?
- [ ] **Right-click `Skin tone ▸` for a two-person emoji**: `Use default tone`, `No tone (yellow)`, then
  `First person ▸` / `Second person ▸`, five tones each (recommended) — or the 26 toned emojis listed
  flat?
- [ ] **Where the tone bar sits**: under the language rows, the whole text column wide, its height
  added to the panel's fixed height (one row, two for two-person emojis) (recommended) — or under the
  48 px emoji in the left column — or left of the copy button, on the first row?
- [ ] **Where the per-emoji tones are kept**: a file of their own, `skin-tones.json` next to the exe,
  like `usage.json` (recommended) — or a `skinTones` object in `settings.json`?
- [ ] **Counters, groups, tray emoji**: counters and groups by the **base text**, shown in the current
  tone; the tray emoji keeps the **exact toned text** picked with `Use as tray icon` (recommended) —
  or the tray emoji by base text too, following the tone — or everything by the exact toned text (a
  toned use counted apart)?
- [ ] **Pre-render**: every variant, once, rendered after the base emojis (recommended — a tone change
  is instant, per-emoji tones cost nothing) — or the tones in use only, the atlas keyed by them?
- [ ] **The name in the details panel**: the base name (`Thumbs up`), the tone shown by the bar
  (recommended) — or the variant's label (`Thumbs up: medium skin tone`)?
- [ ] **Tests**: checked on the built dll by reflection, no test project, like the previous workfiles
  (recommended) — or create a test project?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-09

Initial design, from the **Skin tones** row of `TODO-FEATURES.md` and the step 5 scoping: a default
tone and a per-emoji tone, the default picked in the details panel, two colours there for two-person
emojis.

Exploration (one scout pass, three read-only agents, the subject judged straightforward): the
Emojibase `skins` arrays (330 emojis, 2,030 variants, 380 of them mixed; no tone field — the tones are
read from the text); the details panel is custom-drawn with a fixed height over the whole catalog,
the right-click menu builds submenus like `Add to ▸`; the counters, groups, tray emoji and the
pre-render cache are all keyed by the exact text. Design: tones resolved at display, insertion and
copy, the stores kept on the base text; nine points left open.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | |
| README | | | `README.md` and `README.fr.md` |
| RULES.md | | | § Categories and Insertion, § Details Panel, § Window and Tray Icon (right-click menu), § Size (`settings.json` keys), § Reset All Settings |
| GLOSSARY | | | A *Skin tone* term (*teinte de peau*), maybe *Default tone* — `GLOSSARY.md` and `GLOSSARY.fr.md` |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | How does the user pick a skin tone? | Both: a default tone, and a tone per emoji overriding it | 2026-10-09 |
| 2 | Where does the default tone choice live? | In the details panel at the bottom, showing the selected emoji | 2026-10-09 |
| 3 | Two-person emojis with two tones (handshake, couples)? | The details panel then shows two colours | 2026-10-09 |
| 4 | Is the subject straightforward, or tricky / long? | Straightforward — one exploration pass | 2026-10-09 |
| 5 | What does the details panel's tone bar set, and when is it shown? | | |
| 6 | Two-person emojis: what do the panel's two bars set? | | |
| 7 | Right-click `Skin tone ▸` for a two-person emoji: which shape? | | |
| 8 | Where does the tone bar sit in the details panel? | | |

---

*Last updated: 2026-10-09*
