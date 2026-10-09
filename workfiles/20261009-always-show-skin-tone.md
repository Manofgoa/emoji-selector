# Always Show Skin Tone

> Working document — a settings menu option keeping the details panel's tone swatch shown whatever
> the selection, on by default.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the details panel's **tone swatch** — the default skin tone's colour in a circle, under the copy
button — shows **only while the selection has variants** (`EmojiDetailsPanel.ShowsTone`, RULES.md
§ Skin Tones › Default tone). To change the default tone, the user must first select a person, hand
or body-part emoji.

This workfile adds a **setting** in the ⚙ menu, `Always show skin tone`, **on by default**: while on,
the swatch shows **whatever the selection**, and stays clickable; off, it behaves as today.

Origin: a direct user request (2026-10-09), an adjustment of
[20261009-skin-tones.md](20261009-skin-tones.md). No row of [TODO-FEATURES.md](TODO-FEATURES.md)
matches it — the *Skin tones* row is already marked with that workfile.

Agreed at scoping (Q&A 1–4):

| Point | Decision |
|---|---|
| App | **emoji-selector** — the session moved there from image-grid-fusion |
| The swatch while on, the selection without variants or no selection at all | **Shown and clickable**: the default tone's colour, its tooltip, its drop-down menu of the six tones — even with an empty panel (a search with no result) |
| Menu item | **`Always show skin tone`**, a checked item in the settings menu's *details panel* section, after `Highlight color…`; **checked by default** |
| Exploration depth | Straightforward — a single pass |

Components: `UI/EmojiDetailsPanel.cs` (`ShowsTone`, the paint, the hit test, the tooltip),
`UI/MainForm.cs` (`CreateSettingsMenu`, the setting applied at launch), `Data/SettingsFile.cs` (a new
key), RULES.md, README.md / README.fr.md.

---

## Behaviour

### The setting

- **`Always show skin tone`**, in the settings menu's *details panel* section, after
  `Highlight color…` (`MainForm.CreateSettingsMenu`): checked while on; a click turns it off or on
  again, applied at once, and saved. The window stays.
- **Default: on** — a missing, unreadable or invalid value reads as on, like `showFrench`.
- Kept in `settings.json` as **`alwaysShowSkinTone`** (`SettingsFile.ReadAlwaysShowSkinTone` /
  `WriteAlwaysShowSkinTone`), next to `showFrench`; a write keeps the keys it does not know.
- **Reset all settings…** deletes `settings.json`: the setting comes back on with everything else —
  nothing to add to `AppReset`.

### The swatch

| `Always show skin tone` | Selection | Swatch |
|---|---|---|
| On | Any emoji, with or without variants | Shown, clickable |
| On | None (a search with no result: the panel is empty) | Shown, clickable |
| On | — the emoji data has **no emoji with variants** at all | Shown, clickable — the tone picked applies to no emoji until the data has some |
| Off | An emoji with variants | Shown, clickable (today's behaviour) |
| Off | An emoji without variants, or none | Hidden |

- Shown, it is **the same swatch** everywhere: the **default** tone's colour (yellow for `None`), the
  copy button's hover colour while hovered, its tooltip `Skin tone: Medium`, a click opening the
  tones' drop-down menu under it (`ToneSwatchClicked` → `MainForm.ShowToneMenu`).
- Its **place does not move**: under the copy button's place, as large as it — with no selection,
  where the copy button would be is left empty, the swatch alone below it (Q&A 5).
- **Room for the swatch**: the panel keeps it while the catalog has variants **or** the setting is on
  (`EmojiDetailsPanel.RightColumnHeight`). With today's emoji data — which has variants — toggling the
  setting never changes the panel's height, so never moves the grid.
- A **catalog with no variant at all** (`hasVariants` false — an emoji data version without `skins`):
  the swatch is **shown while the setting is on** (Q&A 7), the room kept for it; off, neither. Only in
  that case does toggling the setting change the panel's height — fitted again like `ShowFrench`
  does (`RequestHeight`).
- The tooltip and the hovered part are refreshed when the setting changes under a still mouse, as
  `ShownEmoji` does today.

### Code shape

- `EmojiDetailsPanel.AlwaysShowTone` (bool, default true): set, it fits the height again
  (`RequestHeight`), repaints, refreshes the hovered part and the tooltip.
- `ShowsTone` becomes: `AlwaysShowTone` **or** the shown emoji has variants.
- `RightColumnHeight` keeps the swatch's room while `hasVariants` **or** `AlwaysShowTone`.
- `OnPaint` returns early with no selection today: the swatch is painted **before** that return when
  `ShowsTone`, so an empty panel still draws it.
- `UpdateToolTip`'s tone case no longer requires a shown emoji; `HitTest` gives the copy button only
  while an emoji is shown, the swatch whenever it shows.
- `UpdateHovered` — the part under a still mouse and its tooltip — is shared by `ShownEmoji` and
  `AlwaysShowTone`.
- `MainForm`: the panel built with `AlwaysShowTone = SettingsFile.ReadAlwaysShowSkinTone()`, the item
  checked in the menu's `Opening` handler, `SetAlwaysShowSkinTone(bool)` applying and saving — the
  `SetShowFrench` pattern.

### Documentation

- **RULES.md**: § Window and Tray Icon (the actions table: a `Settings button ⚙ → Always show skin
  tone` row; the settings menu's *details panel* section listing it), § Size (`settings.json`'s keys:
  `alwaysShowSkinTone`), § Details Panel (*Right*: the swatch, see *Skin Tones*), § Skin Tones ›
  Default tone (the swatch shown while the selection has variants **or** the setting is on).
- **README.md / README.fr.md**, in the same commit: the gear's menu sentence (*details panel*
  section) and the *Skin tones* bullet ("While one of them is selected…" → always, unless the
  setting is off).
- **GLOSSARY.md**: unchanged — the tone swatch is no glossary term; the *Default tone* row already
  says it is picked from the swatch.

---

## Test Impact

The app has **no unit test project** (`src/` holds `EmojiSelector` only): nothing to create or update.
The run checks the behaviour by hand, per RULES.md § Skin Tones' agent check — `settings.json` written
in a **copy** of the build folder (`alwaysShowSkinTone` true / false, `skinTone`), the exe launched,
a search typed into the box (`WM_SETTEXT`) for an emoji without variants and for no result, the window
captured (`PrintWindow`).

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none: no test project | — | — |

---

## Open Questions

- [x] ~~With no selection (a search with no result), the copy button is not drawn: does the swatch stay
  **alone at its place**, under the empty slot of the copy button (recommended: nothing moves), or
  move up into that slot?~~ → At its place: nothing moves
- [x] ~~With a catalog holding **no variant at all** (an emoji data version without `skins`), is the
  swatch still shown when the setting is on (recommended: **no** — no emoji could take the tone, and
  the panel keeps no room for it)?~~ → **Yes**, shown: the room is kept while the setting is on

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-09

Initial design from the user's request ("propose to always show the swatch, as a global setting,
default: always show") and the scoping answers (Q&A 1–4): a checked item `Always show skin tone` in
the settings menu's *details panel* section, on by default, saved as `alwaysShowSkinTone`; while on,
the swatch is shown and clickable whatever the selection, an empty panel included; off, today's
behaviour. Single exploration pass done directly (one chained question: where the swatch is shown,
how a ⚙ setting is stored): the room is already kept, so no height change; no test project. Two
open questions left: the swatch's place in an empty panel, a catalog without variants.

### Iteration 2 — 2026-10-09

Q&A 5 answered: with no selection, the swatch stays at its place under the empty slot of the copy
button — nothing moves. Q&A 6 (a catalog without variants) was not understood: asked again in
simpler words (Q&A 7).

### Iteration 3 — 2026-10-09

Q&A 7 answered, against the recommendation: with emoji data holding no emoji with variants, the
swatch **is shown** while the setting is on. The panel therefore keeps the swatch's room while the
catalog has variants **or** the setting is on; in that edge case only, toggling the setting changes
the panel's height, fitted again through `RequestHeight` as `ShowFrench` does. No open question left.

### Iteration 4 — 2026-10-09 — ✅ Implemented

Go given: code, tests and documentation, in a worktree (`.claude/worktrees/always-show-skin-tone`,
branch `feature/always-show-skin-tone`). The scope is frozen as the sections above stand.

### Iteration 5 — 2026-10-09 — 🧭 Implementation choices

No rule broken. The choices the frozen design did not state:

- **Branch**: the worktree's own, `feature/always-show-skin-tone` — no Branch Gate.
- **`UpdateHovered`**: the hovered part and tooltip refresh, extracted from `ShownEmoji`'s setter so
  `AlwaysShowTone` shares it.
- **Paint order**: the swatch is painted right after the top line, before the empty-selection
  return — one call instead of two.
- **`HitTest`**: the early return on no selection is gone; the copy button needs a shown emoji, the
  swatch only `ShowsTone`.
- **README**: the setting is named twice, as the other details panel settings are — in the *Window*
  bullet's menu sentence and in the *Skin tones* bullet, which says what it changes.
- **Check**: by hand, as the Test Impact planned — two copies of the build, `settings.json` with the
  setting on (missing key) and off, a `medium` default tone; searches `thumbs` (variants), `cat` (no
  variants) and `zzqqxx` (no result) sent with `WM_SETTEXT`, the window captured with `PrintWindow`.
  All six cases as designed: on → the swatch on 👍, on 🐱 and alone in the empty panel; off → on 👍
  only. The check instances were started and ended inside one PowerShell command (a script waiting
  for them), so the *Never `Start-Process`* rule — about the delivery launch surviving the command —
  is not concerned.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 4 | 2026-10-09 | `SettingsFile` key, `EmojiDetailsPanel.AlwaysShowTone`, the ⚙ menu item; checked by hand (Iteration 5) |
| Unit tests | 4 | 2026-10-09 | No test project in the app — not applicable |
| README | 4 | 2026-10-09 | `README.md` / `README.fr.md`: the gear's menu sentence, the *Skin tones* bullet |
| RULES.md | 4 | 2026-10-09 | § Window and Tray Icon (table, menu sections), § Size (keys), § Details Panel (height), § Skin Tones › Default tone |
| Merge | 5 | 2026-10-09 | `feature/always-show-skin-tone` merged into `main` (`--no-ff`); the worktree and the branch removed |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | The tone swatch lives in emoji-selector: where does the workfile go? | emoji-selector, the session moved there | 2026-10-09 |
| 2 | Setting on, the selection without variants or none: what does the swatch do? | Shown and clickable, even with no selection | 2026-10-09 |
| 3 | Which label and place in the ⚙ menu? | `Always show skin tone`, *details panel* section after `Highlight color…`, checked by default | 2026-10-09 |
| 4 | Expected exploration depth? | Straightforward — a single pass | 2026-10-09 |
| 5 | With no selection, the swatch alone at its place or moved up into the copy button's slot? | At its place — nothing moves | 2026-10-09 |
| 6 | A catalog with no variant at all: the swatch shown when the setting is on? | Not understood — to be asked again in simpler words | 2026-10-09 |
| 7 | (Q6 reworded) If the emoji list has no emoji that can change colour, should the colour circle still show? | Yes, shown — the user adds it is the very reason of the feature: the swatch shows whether or not an emoji can take the tone | 2026-10-09 |

---

*Last updated: 2026-10-09*
