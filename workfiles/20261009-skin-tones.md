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

Agreed scope (step 5 scoping, Q&A 1–4, then Q&A 5–8, revised by Iteration 6 and Q&A 14):

| Point | Decision |
|---|---|
| How a tone is picked | **Both**: a **default tone** for every emoji that has variants, and a **tone of its own** per emoji, overriding the default — kept when the default changes |
| Where the default tone is picked | In the **details panel**, at the bottom of the window — **one swatch** under the copy button, shown while the selection has variants, opening a **drop-down menu** of the six tones |
| Two-person emojis (🤝, 🧑‍🤝‍🧑, 💏, 👫…) | The default tone applies to **both persons** |
| A two-person emoji's own tone | Right-click `Skin tone ▸` → `First person ▸` / `Second person ▸` |

The emoji keeps its **base text** everywhere it is stored or looked up; the tone is applied only when
it is **shown, inserted or copied** (see *Shown Text*).

Components: `Data/EmojiCatalog.cs` (reading `skins`), `Data/Emoji.cs` (the variants), a new
`Data/SkinTone.cs` (the tones, the shown text), a new `Data/SkinToneChoices.cs` (the per-emoji tones,
`skin-tones.json`), `UI/EmojiDetailsPanel.cs` (the tone swatch), `UI/MainForm.cs` (the right-click
menu, insertion, the stores, the tray emoji), `UI/EmojiGrid.cs` (the shown text, the pre-render list),
`Data/SettingsFile.cs`, `Data/AppReset.cs`; README / RULES / GLOSSARY.

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

### Reading the variants

- `EmojiCatalog.Entry` gains `skins` (an `Entry[]`). `Emoji` gains **`Variants`**: one `SkinVariant`
  per entry of `skins` — its text (`unicode`) and its tones read from the text: one modifier → the
  same tone for both persons; two → (first, second). A variant holding no modifier, or more than two,
  is left out.
- **Two-person emoji** (`Emoji.IsTwoPerson`): one of its variants has two **different** tones.
- The variants' labels are not kept: the details panel shows the base emoji's names (see *Details
  Panel*).

---

## Tones

- **Six values** (`SkinTone`): **None** (the yellow emoji, no modifier), **Light**, **Medium-light**,
  **Medium**, **Medium-dark**, **Dark**. In files: `"none"`, `"light"`, `"medium-light"`, `"medium"`,
  `"medium-dark"`, `"dark"`.
- Colours of the swatches (Fluent Emoji's palette): None `#FFC83D`, Light `#F7D7C4`, Medium-light
  `#D8B094`, Medium `#BB9167`, Medium-dark `#8E562E`, Dark `#613D30`.
- An emoji without variants (🍎, 😀) is never affected.

### Shown Text

An emoji's **shown text** — what the grid draws, what is inserted, what the copy button copies, what
the details panel draws large — is resolved from its base emoji (`SkinTones.ShownText`):

1. No variants → the base text.
2. A **tone of its own** (see below) → that one: a tone (one-person) or a pair (two-person); None →
   the base text.
3. Otherwise the **default tone**: None → the base text; else the variant of (default, default) — for
   both persons of a two-person emoji.
4. No variant holds the tones asked for → the base text.

### Default tone

- Picked from the details panel's tone swatch (see *Details Panel*), saved in `settings.json` as
  **`skinTone`** (missing or unknown → None).
- A change applies at once to **every section**: categories, frequent, custom groups, search
  results — and the details panel. An emoji with a tone of its own keeps it.

### Tone of its own

- Set per emoji from its **right-click menu**, `Skin tone ▸` after `Use as tray icon` (in every
  section, search results included), only on an emoji that has variants:

  | One-person emoji | Two-person emoji |
  |---|---|
  | `Use default tone` — checked while the emoji has no tone of its own | `Use default tone` — same |
  | separator | `No tone` (yellow) — checked while its own tone is None |
  | `No tone`, `Light`, `Medium-light`, `Medium`, `Medium-dark`, `Dark` — each with its swatch, the emoji's own one checked | separator |
  | | `First person ▸` and `Second person ▸` — the five tones each, with their swatches, the emoji's own pair checked |

- A click on `First person ▸` → a tone: the pair becomes (that tone, the second person's tone **shown
  now** — or that tone when the emoji is shown yellow); `Second person ▸` alike. No confirmation; the
  window stays.
- Reachable from the keyboard too: the right-click menu opens with the Menu key / Shift+F10.
- **`skin-tones.json`**, next to the exe (`Data/SkinToneChoices.cs`), like `usage.json`: a JSON object
  keyed by the **base emoji's text**, the value a tone (`"medium"`, `"none"`) or a pair (`["light",
  "dark"]`), the emojis as their text (`EmojiUsage.ReadableEmojis`). Read once at launch — missing
  or invalid → no tone of its own; an invalid value is skipped. Written after each change, through
  `skin-tones.json.tmp` then a replace; a folder that cannot be written keeps them in memory. An
  emoji the catalog no longer has, and a value that cannot be read, stay in the file and are ignored.

---

## Details Panel

- The **tone swatch**, under the copy button, the same size: a round swatch of the **default tone**
  (yellow for None), on the copy button's hover colour while hovered. Shown only while the selection
  has variants. Its tooltip: `Skin tone: Medium`.
- A click opens a **drop-down menu** under it, its right edge on the swatch's: the six tones — `No
  tone`, `Light` … `Dark` —, each with its swatch (`MainForm.SetSwatch`), the default one checked. A
  choice sets the default tone at once and saves it; the panel and the grid follow. The panel raises
  `ToneSwatchClicked`; `MainForm` builds and shows the menu, as for the settings button.
- The swatch shows the **default** tone, not the selection's own: the emoji drawn large shows the tone
  in use.
- Custom-drawn and hit-tested in `EmojiDetailsPanel`, like the copy button. The panel stays not
  selectable: the swatch is for the mouse; the keyboard sets an emoji's own tone through its
  right-click menu.
- **Room**: the right column stays the button's width. The **fixed height** counts it: button, gap,
  swatch when the catalog has variants — whatever the selection, so moving it never moves the grid.
- The emoji drawn at 48 px is the **shown text**; the copy button copies it; its tooltip stays the
  first code point (`U+1F44D`: the modifier comes later in the sequence).
- Names and tags: the **base emoji's** (`Thumbs up`) — the variants have no tags; the tone is seen in
  the emoji and the swatch.

---

## Stores Keyed by Text

- **Counters** (`usage.json`) and **custom groups** (`custom-groups.json`): the **base text**. A use of
  👍🏽 counts for 👍, a group holds 👍; both are shown in their current tone. The existing files keep
  working: their texts are base texts today.
- **Tray emoji** (`trayEmoji`): the **exact shown text** picked with `Use as tray icon` — a toned one
  included (👍🏽 stays 👍🏽 when the default tone changes). Checked in the menu while it equals the
  emoji's shown text. Reloaded at launch when it is a catalog emoji **or one of its variants**; else
  🙂, as today.

---

## Search

- `EmojiSearch` matches the base emojis' keywords: **no entry per variant** — 2,030 more would give
  every result six times. The results are shown in their tone like any section.
- The tone names (*medium skin tone*, *peau mate*) are not added as keywords.

---

## Pre-render Cache

- The grid draws only what `EmojiBitmapCache` pre-rendered, and the key hashes the whole list.
- The list becomes **every base emoji, in grid order, then every variant**, in catalog order — fixed,
  whatever the tones: a tone change is instant, the key does not move with it. About 3,980 emojis: the
  atlas about twice as tall.
- The first launch after the change renders them all once (the key's emoji list changed: no
  `FormatVersion` bump); until the variants are reached, a toned emoji is green.
- The grid draws each cell's **shown text** (`EmojiGrid.ShownText`, a function `MainForm` sets).

---

## Reset All Settings

- `skinTone` and `secondSkinTone` go with `settings.json`. `skin-tones.json` and its temporary file
  get their row in `AppReset` (`SkinToneChoices.FileName` / `TemporaryFileName`), and the
  confirmation's list names the skin tones.

---

## Test Impact

No test project: checked on the built app, by reflection on the dll, like the previous workfiles (Q&A
13). The behaviours each check must prove:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| The catalog reads `skins`: 330 emojis with variants, 5 for 👍, 25 for 🧑‍🤝‍🧑, none for 🍎 | Reflection script (scratchpad) | Create |
| A variant's tones are read from its text: 👍🏽 → Medium; 🧑🏻‍🤝‍🧑🏼 → (Light, Medium-light) | Reflection script | Create |
| Shown text: default Medium → 👍🏽; own Dark on 👍 → 👍🏿 whatever the default; 🍎 unchanged; default None → 👍 | Reflection script | Create |
| Two-person shown text follows the pair (🤝 Dark + Medium-dark → `1FAF1-1F3FF-200D-1FAF2-1F3FE`), second default missing → same as the first | Reflection script | Create |
| `skinTone` / `secondSkinTone` read / written in `settings.json`; missing or unknown → None / same | Reflection script | Create |
| `skin-tones.json`: round trip, keyed by the base text, tones and pairs, an unknown emoji kept | Reflection script | Create |
| `AppReset` deletes `skin-tones.json` | Reflection script | Create |
| The pre-render list: base emojis then variants, each once | Reflection script | Create |

**Checks run** (Iteration 5): `check-skin-tones.ps1` in the session's scratchpad, on a copy of the build
folder — **44 checks, 0 failure**: 330 emojis with variants, 2,030 variants, 19 two-person emojis;
the tones read from the text; the shown text (default, own tone, None, 🍎, 🤝's mixed pair, the second
default and its fallback, a one-person emoji ignoring it); `settings.json` round trips and fallbacks;
`skin-tones.json` round trips, an unknown emoji and an unreadable value kept, a pair written as an
array, a removal; the pre-render list (count, base emojis first, variants after, each once);
`AppReset` deleting `skin-tones.json`. On screen: the build launched with `skinTone` = `medium`, the
search box filled by `WM_SETTEXT` (`thumbs`, `holding hands`, `handshake`) and the window captured
(`PrintWindow`): the grid and the panel in the medium tone, one bar for 👍, two for 🧑‍🤝‍🧑 and 🤝.

---

## Open Questions

- [x] ~~**What the details panel's tone bar sets**~~ → The **default tone**, the bar shown only while
  the selection has variants (Q&A 5).
- [x] ~~**Two-person emojis in the panel**~~ → The second bar is a **second default tone**, for the
  second person (`secondSkinTone`, the same as the first until changed) (Q&A 6). *(revised 2026-10-09,
  see Iteration 6: one tone for both persons, no second bar)*
- [x] ~~**Right-click `Skin tone ▸` for a two-person emoji**~~ → `Use default tone`, `No tone`, then
  `First person ▸` / `Second person ▸`, five tones each (Q&A 7).
- [x] ~~**Where the tone bar sits**~~ → **Under the copy button** (Q&A 8); two rows of three swatches,
  the second bar under the first (decided by the agent, Q&A 9). *(revised 2026-10-09, see Iteration
  6: one swatch, a drop-down menu)*
- [x] ~~**Where the per-emoji tones are kept**~~ → `skin-tones.json` next to the exe (decided by the
  agent, Q&A 10).
- [x] ~~**Counters, groups, tray emoji**~~ → Counters and groups by the base text; the tray emoji keeps
  the exact toned text picked (decided by the agent, Q&A 11).
- [x] ~~**Pre-render**~~ → Every variant, once, after the base emojis (decided by the agent, Q&A 11).
- [x] ~~**The name in the details panel**~~ → The base name (decided by the agent, Q&A 12).
- [x] ~~**Tests**~~ → Reflection on the built dll, no test project (decided by the agent, Q&A 13).

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

### Iteration 2 — 2026-10-09

First batch of open points answered (Q&A 5–8): the panel's bar sets the default tone and shows only
on an emoji with variants; a two-person emoji's second bar is the second person's default tone; the
right-click menu splits a two-person emoji's own tone by person; the bar sits **under the copy
button** — not under the language rows as proposed.

### Iteration 3 — 2026-10-09

The user asked, mid-design, to finish the analysis, **decide the remaining points alone** and run the
implementation in a separate worktree, the questions kept for after the run. Decided by the agent
(Q&A 9–13), each the option recommended in Iteration 1, to be reviewed:

- The bar under the copy button: **two rows of three** swatches (a single column would make the panel
  much taller, one row of six would narrow the text column a lot); the second bar under the first,
  its first slot "same as the first person".
- Per-emoji tones in **`skin-tones.json`**, next to the exe.
- Counters and groups by the **base text**; the tray emoji keeps the **exact toned text**.
- **Every variant pre-rendered** after the base emojis: the key does not follow the tones.
- The panel shows the **base name**.
- Checks by **reflection on the dll**, no test project.

### Iteration 4 — 2026-10-09 — ✅ Implemented

Go given by the user's message (*finish the analysis, then run the development in a separate
worktree*). Scope taken: **code, checks and documentation** (README, RULES, GLOSSARY) — the user named
no scope, the previous runs delivered all three, and RULES.md must describe what the app does. Where:
a new worktree, branch `feature/skin-tones`, created from this branch's `HEAD`.

### Iteration 5 — 2026-10-09 — 🧭 Implementation choices

⚠️ **Rule broken — propagating a changed imported rule file** (the user's global `CLAUDE.md`, *Rule
changes*): `RULES.md`, imported by the app's `CLAUDE.md`, gained a *Skin Tones* section, but the
other running sessions were **not** messaged. The change lives on `feature/skin-tones` only: the other
sessions' checkouts do not have it, and asking them to re-read their own `RULES.md` would apply
nothing. To be propagated when the branch is merged. Autonomous run: reported instead of asked.

Other choices the design did not state:

- **A thin line between the two tone bars** (a bar gap above and under it): seen on the first capture,
  the two bars read as one block of twelve swatches.
- **An unreadable value of `skin-tones.json` is kept** in the file, ignored — like an unknown emoji
  — rather than dropped: the store keeps the parsed `JsonObject` and writes it back whole.
- **A variant whose tones repeat an earlier one's is left out**, and the pre-render list is made
  distinct: neither happens in Emojibase 17, both guard a hand-edited or future file.
- **The tray emoji at launch**: accepted when it is a catalog emoji **or any variant's text** (a set
  built once, `MainForm.variantTexts`).
- **A two-person emoji's `No tone`** stores `none` (the pair None, None) — the yellow emoji whatever
  the default tones.
- **Swatch details**: 12 logical pixels, a 4-pixel gap, the ring 2 pixels wide in the accent colour, a
  hovered swatch on `SystemColors.ControlLight` like the copy button; a dimmed bar drawn at alpha 80.
- **The check instance's `settings.json`** (written for the capture) was deleted afterwards: the build
  folder is left as the user had it, its `cache\` aside (rendered with the variants, reused at the
  delivery launch).

### Iteration 6 — 2026-10-09 — ⚙️ Post-implementation — One swatch and a drop-down

The user, testing the delivery: *"I'd rather have a single colour shown, then a drop-down menu. And
the chosen colour should apply to every emoji that offers one."* Planned:

- The details panel shows **one swatch** — the default tone — under the copy button, in place of the
  two bars of six; a click opens a **drop-down menu** under it listing the six tones, each with its
  swatch, the one in use checked. A choice sets the default tone, saved as today (`skinTone`).
- **One colour for every emoji that has variants**, two-person ones included: both persons take the
  default tone. The second bar and `secondSkinTone` go (the key is no longer read; a mixed pair
  stays possible through the right-click menu).
- The panel's right column narrows back to the button's width; its height counts one swatch.
- The emojis that have a tone of their own **keep it** (Q&A 14): the right-click `Skin tone ▸` stays
  as it is.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 4, 5 | 2026-10-09 | `Data/SkinTone.cs`, `Data/SkinToneChoices.cs` (new); `Emoji`, `EmojiCatalog`, `SettingsFile`, `AppReset`; `EmojiGrid`, `EmojiDetailsPanel`, `MainForm` |
| Unit tests | 5 | 2026-10-09 | No test project — 44 reflection checks on a copy of the build, all passing (*Test Impact* § Checks run) |
| README | 5 | 2026-10-09 | `README.md` and `README.fr.md`: *Skin tones*, tray icon, categories, reset, planned |
| RULES.md | 5 | 2026-10-09 | New § Skin Tones; § Window and Tray Icon (menu, tray emoji), § Size (`settings.json` keys), § Categories and Insertion, § Reset All Settings, § Details Panel |
| GLOSSARY | 5 | 2026-10-09 | *Skin tone* (*teinte de peau*), *Default tone* (*teinte par défaut*) — `GLOSSARY.md` and `GLOSSARY.fr.md` |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | How does the user pick a skin tone? | Both: a default tone, and a tone per emoji overriding it | 2026-10-09 |
| 2 | Where does the default tone choice live? | In the details panel at the bottom, showing the selected emoji | 2026-10-09 |
| 3 | Two-person emojis with two tones (handshake, couples)? | The details panel then shows two colours | 2026-10-09 |
| 4 | Is the subject straightforward, or tricky / long? | Straightforward — one exploration pass | 2026-10-09 |
| 5 | What does the details panel's tone bar set, and when is it shown? | The default tone, shown only while the selection has variants | 2026-10-09 |
| 6 | Two-person emojis: what do the panel's two bars set? | A second default tone, for the second person, following the first until changed | 2026-10-09 |
| 7 | Right-click `Skin tone ▸` for a two-person emoji: which shape? | By person: `Use default tone`, `No tone`, `First person ▸` / `Second person ▸` | 2026-10-09 |
| 8 | Where does the tone bar sit in the details panel? | Under the copy button | 2026-10-09 |
| 9 | How are the swatches laid out under the copy button? | Not asked — decided by the agent (Iteration 3): two rows of three, the second bar under the first | 2026-10-09 |
| 10 | Where are the per-emoji tones kept? | Not asked — decided by the agent: `skin-tones.json` | 2026-10-09 |
| 11 | Counters, groups, tray emoji; pre-render? | Not asked — decided by the agent: base text, the tray's toned text kept; every variant pre-rendered | 2026-10-09 |
| 12 | The name in the details panel? | Not asked — decided by the agent: the base name | 2026-10-09 |
| 13 | Tests? | Not asked — decided by the agent: reflection on the dll | 2026-10-09 |
| 14 | A tone chosen in the panel: what happens to the emojis that have a tone of their own (right-click)? | They keep it | 2026-10-09 |

---

*Last updated: 2026-10-09*
