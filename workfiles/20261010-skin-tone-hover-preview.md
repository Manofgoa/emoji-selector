# Skin Tone Hover Preview

> Working document — the skin-tone menus preview the highlighted tone live, in the grid, the details panel's
> emoji and the tone swatch, before anything is chosen.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today a skin tone is seen only once it is clicked: the menu closes, the emojis are drawn again. This task makes
both skin-tone menus **preview** the tone under the highlight — moved by the mouse or the arrow keys — in real
time, and put the real tones back when the menu closes without a choice.

- **The menus**: the details panel's tone menu (default tone, `MainForm.ShowToneMenu`) and the emoji's right-click
  `Skin tone ▸` submenu (own tone, `MainForm.CreateSkinToneMenu`), its `First person ▸` / `Second person ▸`
  submenus included.
- **What follows the preview**: the grid's emojis, the details panel's large emoji, and the tone swatch (default
  tone only).
- **Nothing saved** by a preview: `settings.json` and `skin-tones.json` are written by a click only, as today.

Relevant code: `UI/MainForm.cs` (`ShowToneMenu`, `CreateSkinToneMenu`, `CreateToneItem`, `ShownText`,
`OnTonesChanged`, `ShowOnce`), `UI/EmojiDetailsPanel.cs` (`DefaultTone`, `RefreshShownEmoji`), `Data/SkinTone.cs`
(`SkinTones.ShownText`, unchanged).

---

## Preview State

- `MainForm` keeps a **preview** (`tonePreview`), separate from the real tones (`skinTone`, `skinToneChoices`), at
  most one at a time — a `DefaultTonePreview` or an `OwnTonePreview`, each **owned by its highlighted item**: an item
  losing the highlight clears the preview only while it still owns it, so the next item's preview is never undone:

  | Preview | Holds | Applies to |
  |---|---|---|
  | Default tone | a `SkinTone` | every emoji with variants **without an own tone** — the ones with an own tone keep it, as `SetDefaultTone` does |
  | Own tone | the emoji's text, and a `SkinTonePair?` — `null` for *Use default tone* | that emoji only |

- **`MainForm.ShownText`** — the grid's and the details panel's `ShownText` — resolves through the preview while
  one is set: `SkinTones.ShownText(emoji, own, previewedDefault)` for a default-tone preview,
  `SkinTones.ShownText(emoji, previewedOwn, skinTone)` for the previewed emoji of an own-tone preview. No preview →
  as today.
- The insertion, the copy, `Use as tray icon` and the menus' check marks keep the **real** tones: a menu being open,
  none of them can run during a preview, and the check marks are computed when the menu is built.

---

## Triggers

- **The highlight**: a tone item becoming the highlighted item of its menu — the mouse over it, or the arrow keys
  reaching it — sets the preview to its tone. The mechanism is `ToolStripItem.SelectedChanged`, raised for the mouse
  and the keyboard alike (`MainForm.PreviewWhileHighlighted`, hooked on every tone item and on `Use default tone`).

  | Highlighted item | Preview |
  |---|---|
  | Tone menu (swatch): `No tone`, `Light` … `Dark` | Default tone = that tone |
  | `Skin tone ▸`: `No tone`, `Light` … `Dark` (one person) | Own tone of the emoji = that tone |
  | `Skin tone ▸`: `No tone` (two persons) | Own tone = `None` |
  | `First person ▸` / `Second person ▸`: a tone | Own tone = the pair the click would save (the other person keeps its shown tone, or takes this one while the emoji is yellow) |
  | `Skin tone ▸`: `Use default tone` | Own tone of the emoji = `null`: the emoji shown in the default tone |
  | Any other item — `First person ▸` / `Second person ▸` themselves, `Skin tone ▸`, the right-click menu's other items — or none (the mouse off the items) | No preview: the real tones |

- **A click** saves the choice as today (`SetDefaultTone`, `SetOwnTone`), then the preview is cleared: the real
  tones are now the clicked one, nothing flickers.
- **The menu closing without a click** — Esc, a click elsewhere, the window hiding — clears the preview: the real
  tones are drawn again. Nothing is written. Hooked on the root menu's `Closed` (the swatch's menu; the right-click
  menu when it holds `Skin tone ▸`), on top of the items losing the highlight.

---

## Redraw

- Every change of the preview, its clearing included, goes through `OnTonesChanged`: the grid invalidated (every
  variant is pre-rendered, nothing to rebuild — a variant not rendered yet shows the green cell, as today) and the
  details panel's emoji rendered again (`RefreshShownEmoji`) — which ends the copy button's check mark, as a tone
  change does today: accepted.
- **The tone swatch**: during a default-tone preview, `EmojiDetailsPanel.DefaultTone` is set to the previewed tone
  (colour and tooltip follow), and back to the real one when cleared. An own-tone preview leaves it alone — it
  shows the default tone.
- **The details panel's emoji** is the grid's selection: with the right-click menu open, it is the right-clicked
  emoji (the mouse over the menu does not move the selection), so an own-tone preview shows on it too.

---

## Documentation

- `RULES.md` § *Skin Tones* → *Default tone* and *Own tone*: the preview, its triggers, its end, nothing saved.
- `README.md` § *Skin tones* and `README.fr.md`, in the same commit: one sentence — the tones are previewed as the
  menu's highlight moves, and put back if the menu closes without a choice.
- `GLOSSARY.md`: no new term.

---

## Test Impact

**No unit test**: the repository has no test project, and `SkinTones.ShownText` — the one pure function involved —
is unchanged. The preview lives in `MainForm`'s menu handling, which a script cannot drive (RULES.md § *Skin Tones*:
the details panel and its menus are not clickable from a script). It is checked by hand.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — (no test project; checked by hand) | — | — |

---

## Open Questions

- [x] ~~In `Skin tone ▸`, does highlighting **`Use default tone`** preview the emoji in the default tone?~~ → Yes:
  the emoji's own tone previewed as `null`
- [x] ~~When the highlight is on **no tone item** — `First person ▸` / `Second person ▸` themselves, the `Skin tone ▸`
  item, the other items of the right-click menu, or nothing (mouse off the items) — what is shown?~~ → The real
  tones: the preview follows the highlighted tone item only
- [x] ~~The copy button's **check mark** (shown about a second after a copy) is ended by `RefreshShownEmoji`: may a
  preview end it early?~~ → Yes

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-10

Initial design, from the scoping answers (Q1–Q4): both menus preview; the grid, the details panel's emoji and the
tone swatch follow; the mouse and the keyboard highlight trigger it; closing without a click puts the real tones
back and saves nothing. The preview is one state in `MainForm` read by `ShownText`, redrawn through
`OnTonesChanged`. Three open points left: `Use default tone`, the non-tone items, the copy check mark.

### Iteration 2 — 2026-10-10

Open points settled (Q5): highlighting `Use default tone` previews the emoji in the default tone; a highlight on no
tone item shows the real tones; a preview may end the copy button's check mark. No open question left.

### Iteration 3 — 2026-10-10 — ✅ Implemented

Go given: code, unit tests and documentation, in a worktree (`.claude/worktrees/skin-tone-hover-preview`, branch
`feature/skin-tone-hover-preview`). Scope frozen as described above.

### Iteration 4 — 2026-10-10 — 🧭 Implementation choices

- **Highlight mechanism**: `ToolStripItem.SelectedChanged` — public, raised by the mouse and the arrow keys alike —
  rather than `MouseEnter` (mouse only) or a `ToolStripMenuItem` subclass.
- **Preview ownership**: the preview records (`DefaultTonePreview`, `OwnTonePreview`) carry their item; an item losing
  the highlight clears only its own preview, whatever order WinForms raises the old item's and the new one's events in.
- **Clearing on close**: the root menus' `Closed` clears it too — the swatch's menu always, the right-click menu only
  when it holds `Skin tone ▸` (the only case it can preview).
- **The swatch**: `EmojiDetailsPanel.DefaultTone` is set by `SetTonePreview` at every change — the previewed default
  tone, else the real one — so its tooltip follows too.
- No project rule broken; no other divergence from the frozen design.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 4 | 2026-10-10 | `UI/MainForm.cs` — built, 0 warning; checked by hand (the menus cannot be driven from a script) |
| Unit tests | 4 | 2026-10-10 | Not applicable: no test project (see *Test Impact*) |
| RULES.md | 4 | 2026-10-10 | § *Skin Tones* → new *Preview* subsection, pointers from *Default tone* and *Own tone* |
| README | 4 | 2026-10-10 | § *Skin tones*, `README.md` and `README.fr.md` in the same commit |
| Merge | 4 | 2026-10-10 | Merged into `main` (`--no-ff`, no conflict, `main` built after it with the acrylic prototype's merge); worktree and branch removed |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which menus preview the tone on hover? | Both: the tone swatch's menu (default tone) and the right-click `Skin tone ▸` submenu (own tone, the person submenus included) | 2026-10-10 |
| 2 | What changes in real time during the hover? | The grid's emojis, the details panel's large emoji, and the tone swatch | 2026-10-10 |
| 3 | What triggers the preview, and what happens when the menu closes without a choice? | Mouse hover and keyboard highlight; closing without a click puts the real tone back, nothing saved | 2026-10-10 |
| 4 | Straightforward or tricky / long? | Straightforward — a single exploration pass | 2026-10-10 |
| 5 | `Use default tone` previewed? Non-tone items → real tones? Copy check mark ended by a preview? | `Use default tone` previewed in the default tone; non-tone items → the real tones; a preview may end the check mark | 2026-10-10 |

---

*Last updated: 2026-10-10*
