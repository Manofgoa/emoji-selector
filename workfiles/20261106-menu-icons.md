# Menu Icons

> Working document — an icon in front of the menu entries of Emoji Selector, as Image Grid Fusion has them, and a
> rule so every future menu entry gets one.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The app's menus (WinForms `ContextMenuStrip`) show text only today — a ✓ in front of the checked toggles, a swatch in
front of the colour and tone items. This work gives their **actions and toggles** an icon, so they are recognised at a
glance, and writes a rule into `RULES.md` (§ *Icons*) so a future entry follows the same style.

The reference is Image Grid Fusion's `workfiles/20261029-menu-icons.md` and its `RULES.md` § *Icons*: every action and
toggle of every menu shows a **Segoe UI Emoji glyph in full colour**, drawn by Direct2D at 16 logical pixels in device
pixels, never the values of a submenu nor a greyed caption; a checked toggle shows its icon framed in place of the ✓.

Settled at scoping (2026-11-06, Q&A 1–4):

- **Every menu is in scope**: the settings menu ⚙, an emoji's right-click menu, the "…" menus of the frequent section
  and of a group, the tray icon's menu. The tone swatch's menu keeps its swatches (they are its values).
- **Style**: a **Segoe UI Emoji glyph in colour**, rendered by the app's own `Drawing/EmojiRenderer.cs` (Direct2D) —
  GDI would draw it in one colour.
- **A rule in `RULES.md`** for every future menu entry.
- The subject is **straightforward**: one exploration pass.

The user then asked for the choices to be taken by the agent and the implementation run in a worktree, unattended
(Q&A 5): the open points below were decided by the agent, and reported.

---

## Menus Today

| Menu | Built by | Entries |
|---|---|---|
| Settings ⚙ | `MainForm.CreateSettingsMenu` (once, kept) | 18 entries in 7 sections; `Show groups ▸` (group names), `Window opacity ▸` (percentages, `Acrylic`), `Acrylic veil ▸` (percentages); `Highlight color…` with its swatch; `Emoji zoom`, a `ToolStripControlHost` (label + numeric box) |
| An emoji's | `MainForm.ShowEmojiMenu` (one show) | `Use as tray icon`, `Skin tone ▸`, `Add to ▸` (group names), `Remove` / `Remove from frequently used` |
| `Skin tone ▸` | `MainForm.CreateSkinToneMenu` | `Use default tone`, the tones with their swatches; for two persons `No tone`, `First person ▸`, `Second person ▸` |
| The tone swatch's | `MainForm.ShowToneMenu` (one show) | The six tones, each with its swatch |
| The frequent section's "…" | `MainForm` (one show) | `Hide frequently used` |
| A group's "…" | `MainForm.CreateGroupMenu` (one show) | `Rename…`, `Reorder`, `Move up`, `Move down`, `Hide group`, `Delete group` |
| The tray icon's | `UI/TrayIcon.cs` (once, kept) | `Exit` |

- No custom renderer: every menu draws with WinForms' default one (`ToolStripProfessionalRenderer`).
- The only menu images today are swatches (`MainForm.SetSwatch`): drawn at `LogicalToDeviceUnits(16)`, again at each
  opening of the settings menu.
- `Drawing/EmojiRenderer.cs` already draws an emoji in colour into a `Bitmap` of any size, flag images included (the
  tray icon uses it at the small-icon size); one renderer per thread.

---

## Icon Style

- **A Segoe UI Emoji glyph in colour**, rendered by an `EmojiRenderer` of the UI thread at **four times** the icon's
  side, then scaled down (high-quality bicubic) to **16 logical pixels in device pixels** — never a 16 px bitmap
  stretched, never GDI. Each (emoji, size) rendered once, kept in memory for the app's life (`UI/MenuIcons.cs`).
- **`ImageScaling = None`**: the image is already at the device size; the menu's image margin follows it.
- **Which entries**: every **action and toggle**, and every **submenu naming a subject** (`Window opacity ▸`,
  `Add to ▸`, `Skin tone ▸`…). Never the **values** of a submenu (the percentages, `Acrylic`, the group names of
  `Show groups ▸` / `Add to ▸`, `Use default tone` and the tones). A **colour item** keeps its swatch (shared RULES.md
  § Colour Items).
- **One meaning, one icon**: the meaning an icon already has in the app keeps it — the frequent tab's star ⭐, the
  custom tab's heart ❤️ —; an action shown in several menus (`Hide …`, `Remove …`) has one icon.
- **Checked toggles**: WinForms' default look — checked, the icon is drawn in its highlighted frame in place of the ✓
  (Windows' standard). No custom renderer for that.
- **Disabled entries**: their icon greyed by WinForms, as their text.
- **When drawn**: a menu built for one show sets its icons when it is built, at the window's DPI; the settings menu at
  each opening (`Opening`), as its swatch — so it follows the monitor. The tray icon's menu at each opening, at the
  small-icon size (`SystemInformation.SmallIconSize`), as the tray icon itself.

---

## Entries

▢ a toggle, ▸ a submenu (its values without icon).

### Settings ⚙

| Entry | Icon |
|---|---|
| Show frequently used ▢ | ⭐ — the frequent tab's star |
| Clear frequently used | 🧹 |
| New group… | ➕ |
| Show groups ▸ | ❤️ — the custom tab's heart |
| Show emoji sub-categories ▢ | 🗂️ |
| Show special characters ▢ | 🔣 |
| Show special character sub-categories ▢ | 🗂️ — the same meaning as the emojis' |
| Show French names ▢ | 🇫🇷 — a flag image, drawn by `EmojiRenderer` like the grid's flags |
| Highlight color… | its swatch — unchanged |
| Emoji zoom (hosted box) | 🔍 — drawn in the image margin, see § *Emoji Zoom Row* |
| Always show skin tone ▢ | 🎨 |
| Reset window size | 🪟 |
| Window opacity ▸ | 🔅 |
| Acrylic veil ▸ | 🧊 |
| Open app folder | 📂 |
| Start with Windows ▢ | 🚀 |
| Check for emoji updates… | 🔄 |
| Reset all settings… | ♻️ |

### An Emoji's Menu

| Entry | Icon |
|---|---|
| Use as tray icon ▢ | **the right-clicked emoji itself**, as shown (its skin tone) — what the tray icon would show |
| Skin tone ▸ | 🎨 — the same meaning as `Always show skin tone` |
| Skin tone ▸ → First person ▸ / Second person ▸ | 👤 |
| Add to ▸ | ❤️ — the custom groups |
| Remove (from a group) | ➖ |
| Remove from frequently used | ➖ |

### The "…" Menus

| Menu | Entry | Icon |
|---|---|---|
| Frequent section | Hide frequently used | 🙈 |
| A group | Rename… | ✏️ |
| A group | Reorder | 🔀 |
| A group | Move up | ⬆️ |
| A group | Move down | ⬇️ |
| A group | Hide group | 🙈 |
| A group | Delete group | 🗑️ |

### The Tray Icon's Menu

| Entry | Icon |
|---|---|
| Exit | 🚪 |

### Unchanged

The tone swatch's menu (swatches), the tones of `Skin tone ▸` (swatches), `Use default tone`, the percentages and
`Acrylic`, the group names.

---

## Emoji Zoom Row

`Emoji zoom` is a `ToolStripControlHost` (a label and a `PercentUpDown`), not a menu item: it has no `Image`, and the
menu lays it out from the text column — its label starts where the other items' texts do.

- Its icon 🔍 is drawn **in the menu's image margin**, on the row's vertical centre, horizontally centred in the margin
  like the other items' images: the settings menu gets a renderer of its own, `MenuIcons.Renderer` — a
  `ToolStripProfessionalRenderer` whose `OnRenderImageMargin` draws, after the margin, the icon of each control host
  registered with `MenuIcons.SetHostIcon`. The look of the menu is otherwise the default renderer's.

---

## Test Impact

The app has **no unit test project** (`src/` holds `EmojiSelector` only). The pure part — the icon cache's sizes and
reuse — is checked by reflection on the built dll, as `RULES.md` describes for the other features; the menus by a
capture.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| `MenuIcons.Get` gives a bitmap of the asked size, in colour, the same one for the same (emoji, size) | None — reflection check on the dll | — |
| Every action and toggle of the menus has its icon; values and swatches unchanged; checked toggles framed | None — capture of the settings menu (reflection in a `pwsh -Sta` process) and by hand | — |

---

## Open Questions

- [x] ~~Which menus?~~ → All of them (Q&A 1)
- [x] ~~Emoji in colour or Fluent glyphs?~~ → Emoji in colour, by `EmojiRenderer` (Q&A 2)
- [x] ~~A rule in `RULES.md`?~~ → Yes, § *Icons* (Q&A 3)
- [x] ~~Checked toggles: keep the ✓, or the framed icon?~~ → The framed icon, WinForms' default, as Image Grid Fusion
  (decided by the agent, Q&A 5)
- [x] ~~`Use as tray icon`: a fixed icon, or the emoji itself?~~ → The emoji itself, as shown (decided by the agent)
- [x] ~~`Emoji zoom`, a hosted control: an icon?~~ → Yes, 🔍 drawn in the image margin by the settings menu's renderer
  (decided by the agent)
- [x] ~~Icons kept on the disk, as Image Grid Fusion's `icon-cache\`?~~ → No: about 25 small renders at the first
  opening, kept in memory; no new file next to the exe (decided by the agent)
- [x] ~~The submenus' values (percentages, group names, tones)?~~ → No icon, as Image Grid Fusion (decided by the
  agent)
- [ ] The `Emoji zoom` row's label is a point larger than the other entries (the window's 10-point font, not the
  menu's): align it on the menu's font? (seen during the run, Iteration 3)

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-11-06

Initial design, modelled on Image Grid Fusion's menu icons (user's request: "see how it was done in
image-grid-fusion"). Scoping answers (Q&A 1–4): every menu, colour emojis by `EmojiRenderer`, a `RULES.md` rule, a
straightforward subject. The user then asked the agent to take the remaining choices and run the implementation in a
worktree without interaction (Q&A 5): framed icon for checked toggles, the emoji itself for `Use as tray icon`, 🔍 in
the image margin for the zoom row, a memory cache only, no icon on submenu values; the icon table of § *Entries*.

### Iteration 2 — 2026-11-06 — ✅ Implemented

Go given (Q&A 5): code, unit tests and documentation, in a worktree (`feature/menu-icons`).

### Iteration 3 — 2026-11-06 — 🧭 Implementation choices

- **Rule breaches**: none.
- **🪟 and 🔅 in place of 📐 and 🌫️** (`Reset window size`, `Window opacity ▸`): at 16 px, both designed icons came out
  pale grey, hard to see on the menu's background (capture of the settings menu). § *Entries* updated.
- **`MenuIcons` API**: `Get(emoji, size)` (the shared, cached bitmap), `Set(item, emoji, size)` (a copy on the item,
  `ImageScaling = None`, disposed with it; nothing redone when the emoji and size have not changed), `SizeFor(control)`,
  `SetHostIcon(host, emoji, size)` and `MenuRenderer`, a `ToolStripProfessionalRenderer` — the look WinForms already
  used — set on the settings menu only. The emoji constants live in `MenuIcons`, one per meaning.
- **The settings menu's icons** are collected while it is built (a local `WithIcon`) and set in its `Opening`, next to
  the swatch.
- **Tests**: no test project, as § *Test Impact* says. Checked by reflection on a copy of the build in a `pwsh -Sta`
  process: `MenuIcons.Get` gives 24 × 24 for 24, 16 × 16 for 16, the same bitmap twice for the same emoji and size; the
  settings menu, an emoji's menu and a group's menu opened by reflection and drawn with `DrawToBitmap`: every action and
  toggle has its colour icon, the checked toggles framed, the disabled ones greyed (`Acrylic veil ▸`, `Move up` /
  `Move down`), the highlight colour's swatch kept, 🔍 in the image margin on the zoom row's centre, aligned with the
  other icons. A screen capture showed only the wallpaper (session locked): `RULES.md` says so.
- **Seen, not changed** (out of the frozen scope): the `Emoji zoom` row's label is drawn in the window's 10-point font,
  a point larger than the menu's other entries — offered as an Open Question.
- **`RULES.md`**: a new section `## Icons`, before *Repository Docs*. **README**: one clause in the *Window* bullet of
  both READMEs. No glossary term.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3 | 2026-11-06 | `UI/MenuIcons.cs`; icons set in `MainForm` (settings, emoji, skin tone, section and group menus) and `TrayIcon` |
| Unit tests | 3 | 2026-11-06 | No test project — reflection check of `MenuIcons.Get` and `DrawToBitmap` captures of three menus instead |
| RULES.md | 3 | 2026-11-06 | New section *Icons* |
| README | 3 | 2026-11-06 | `README.md` / `README.fr.md`: the *Window* bullet |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which menus get icons? | All: settings ⚙, an emoji's right click, the "…" menus, the tray icon's | 2026-11-06 |
| 2 | Which style? | Colour emojis (Segoe UI Emoji by the app's Direct2D renderer), as Image Grid Fusion | 2026-11-06 |
| 3 | A rule in `RULES.md` for future menu entries? | Yes | 2026-11-06 |
| 4 | Straightforward or tricky / long? | Straightforward | 2026-11-06 |
| 5 | (Go) | "Je vais au dodo, fais les choix et lance les dévs dans un worktree sans interactions avec moi. GO" → code, tests and docs, worktree, choices taken by the agent | 2026-11-06 |

---

*Last updated: 2026-11-06*
