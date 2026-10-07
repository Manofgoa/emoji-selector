# Category Tabs

> Working document — show the emojis grouped by category, with one tab per category, like the
> Windows Win+; panel but handier to use.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The main window shows every emoji in **one continuous scrolling grid**, split into sections — one
per **category** — under a strip of **tabs**. Clicking a tab jumps to its section; scrolling moves
the active tab along. Clicking an emoji triggers the **click action**.

Reference: the Windows Win+; panel (and Twitter's emoji picker, whose categories are tabs).

| In scope | Out of scope (see [TODO-FEATURES.md](TODO-FEATURES.md)) |
|---|---|
| Category tabs, continuous grid, section headers | Recents tab |
| Color rendering of the emojis | Skin tones — only the default (yellow) variant is shown |
| Click action | Search box |
| | Flags tab |

### Starting point

| Fact | Source |
|---|---|
| The app is **WinForms**, `net10.0-windows10.0.19041.0`, no NuGet package | `src/EmojiSelector/EmojiSelector.csproj` |
| `MainForm` is an empty skeleton: title, 420×320 client size, 320×240 minimum | `src/EmojiSelector/UI/MainForm.cs` |
| No test project exists | `src/` |
| A Direct2D/DirectWrite color-emoji renderer exists **uncommitted** in this checkout (`Drawing/Direct2DInterop.cs`), written for the tray icon | [20261007-tray-icon.md](20261007-tray-icon.md) — apparently in progress in another session |
| GDI (`TextRenderer`) and GDI+ (`Graphics.DrawString`) draw Segoe UI Emoji in **monochrome**: color needs Direct2D with `D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT` | [DirectWrite color fonts](https://learn.microsoft.com/windows/win32/directwrite/color-fonts) |
| Segoe UI Emoji has **no flag glyphs**: a flag shows as two letters | Windows behaviour |

---

## Emoji Data

Where the list of emojis, their category and their name come from.

- **Source**: [Emojibase](https://emojibase.dev) data (`emojibase-data`, MIT) — `en/compact.json`
  (or `data.json`) and `meta/groups.json`, **vendored** in the repository and embedded as a resource,
  parsed with `System.Text.Json`. It carries group, subgroup, label, tags (the future search
  keywords) and skin-tone variants.
- Group `component` (skin-tone and hair swatches) is skipped. Skin-tone variants are skipped
  (backlog). Group `flags` is skipped (no Flags tab, backlog).
- Every other emoji is kept, **even one newer than the system font** — it then shows as a box.

### Categories (tabs)

The tabs follow the **Win+; order** — Activities before Travel, unlike Unicode's order. Unicode's
*Smileys & Emotion* and *People & Body* are merged into one tab, as Win+; does.

| # | Tab | Unicode groups |
|---|---|---|
| 1 | Smileys & People | Smileys & Emotion + People & Body |
| 2 | Animals & Nature | Animals & Nature |
| 3 | Food & Drink | Food & Drink |
| 4 | Activities | Activities |
| 5 | Travel & Places | Travel & Places |
| 6 | Objects | Objects |
| 7 | Symbols | Symbols |

Labels in English, like the rest of the app's UI. No Flags tab: Segoe UI Emoji has no flag glyphs
(backlog, see [TODO-FEATURES.md](TODO-FEATURES.md)).

---

## Rendering

- Emojis are drawn in **color** through Direct2D/DirectWrite with the color-font option, with the
  system font (Segoe UI Emoji). Each emoji is rendered once into a cached bitmap, then the grid
  draws the bitmaps.
- **Direct2D layer**: the tray icon's hand-written interop (`Drawing/Direct2DInterop.cs` and its
  `EmojiRenderer`), reused — no NuGet package. It already draws an emoji into a bitmap, which is all
  the grid needs. A move to `Vortice.Direct2D1` is worth it only if the needs grow (drawing straight
  into the window, measuring text, checking glyph coverage…).
- **Prerequisite**: the tray icon's work ([20261007-tray-icon.md](20261007-tray-icon.md)) is
  committed before this implementation starts — it is not yet.
- Emojis newer than the system font are drawn anyway: they render as boxes. No coverage check.

---

## UI

```
┌──────────────────────────────────────┐
│ 😀  🐻  🍔  ⚽  🚗  💡  🔣             │  ← tab strip, active tab underlined
├──────────────────────────────────────┤
│ Smileys & People                     │  ← section header
│ 😀 😃 😄 😁 😆 😅 😂 🤣 😊 😇         │
│ …                                    │  ← one continuous, scrollable grid
│ Animals & Nature                     │
│ 🐶 🐱 🐭 …                            │
└──────────────────────────────────────┘
```

- **Tab strip**: one **monochrome** icon per category, like Win+; — a glyph of Segoe Fluent Icons
  (Windows 11), falling back to Segoe MDL2 Assets (Windows 10) — grey, the active one in the accent
  color and underlined; the category name as a tooltip. Drawn with GDI (a monochrome font needs no
  Direct2D). Custom-drawn, not a `TabControl` (a `TabControl` shows one page at a time).
- **Grid**: a custom-drawn, double-buffered scrolling panel. Fixed-size cells; the number of
  columns follows the window width (reflow on resize). Each section starts on a new row under its
  header.
- **Tab → grid**: clicking a tab scrolls its section header to the top.
- **Grid → tab**: the active tab is the section whose header is the last one above the top of the
  viewport.
- **Hover**: the cell is highlighted; a tooltip shows the emoji's name (Emojibase label).
- **Window size**: the default client size grows to fit about 9 columns (≈ 360×450), the minimum
  size stays.

---

## Click Action

Clicking an emoji **inserts** it into the window that had the focus before the app's window, then
the app's window **hides** — like Win+;.

1. The app remembers the **previous foreground window** when its own window is activated
   (`WM_ACTIVATE`, the handle of the window being deactivated).
2. On click, the window hides; the previous window is brought back to the foreground
   (`SetForegroundWindow`).
3. The emoji is typed into it with `SendInput` and `KEYEVENTF_UNICODE`, one event pair per UTF-16
   code unit of its sequence — no clipboard involved.

- **Hiding** goes through the tray icon's hide path ([20261007-tray-icon.md](20261007-tray-icon.md)):
  the window goes to the tray. Without the tray icon, a hidden window could not come back: the
  window minimizes instead.
- **Caveat**: Windows blocks `SendInput` into an elevated (administrator) window from a
  non-elevated app (UIPI) — the emoji is then not inserted. Not handled.
- No previous window (none remembered, or it was closed): the window hides, nothing is typed.
- Copying to the clipboard (text or image) is **not** a click action: backlog
  ([TODO-FEATURES.md](TODO-FEATURES.md)).

---

## Test Impact

**No unit test**: the app has no test project and none is created — everything is checked by hand,
like the tray icon (Q&A #13).

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — | — | — |

Manual checks:

- 7 tabs in Win+; order; no Flags tab; no skin-tone variant, no component swatch in the grid.
- Emojis in color; a too-recent emoji shows as a box.
- Clicking a tab brings its section header to the top; scrolling moves the active tab.
- Resizing the window reflows the columns; each section starts on a new row.
- Hover highlights the cell and shows the emoji's name.
- Clicking an emoji in front of Notepad: the window hides, the emoji is typed into Notepad.

---

## Open Questions

- [x] ~~Data source: Emojibase vendored, or Unicode `emoji-test.txt`?~~ → Emojibase, vendored
- [x] ~~Direct2D layer: reuse the tray icon's hand-written interop (`Drawing/Direct2DInterop.cs`, not committed yet), or the `Vortice.Direct2D1` NuGet?~~ → The tray icon's interop; the tray icon is committed first
- [x] ~~Flags (no glyphs in Segoe UI Emoji): bundle images for the flags only, keep letter pairs, or drop the Flags tab?~~ → No Flags tab; logged in TODO-FEATURES.md
- [x] ~~Emojis newer than the system font (boxes): hide them, or show them anyway?~~ → Shown anyway
- [x] ~~Click action: copy to the clipboard, insert into the previously focused window, or both?~~ → Insert into the previously focused window
- [x] ~~After a click: the window stays open, or hides?~~ → Hides
- [x] ~~Tab icons: color emojis, or monochrome icons like Win+; (Segoe Fluent Icons)?~~ → Monochrome, like Win+;
- [x] ~~Keyboard navigation in the grid (arrows, Enter): in scope, or backlog?~~ → Backlog
- [x] ~~Unit tests: create an xUnit test project, or check everything by hand like the tray icon?~~ → By hand, no test project

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-07

Initial design from the scoping batch and a scout pass (codebase + web research on emoji
libraries): continuous grid with category tabs in Win+; order, Emojibase as the data candidate,
Direct2D color rendering of the system font, click action in scope, recents / skin tones / search
moved to [TODO-FEATURES.md](TODO-FEATURES.md).

### Iteration 2 — 2026-10-07

First batch of answers: Emojibase is the data source; the Flags tab is dropped (no flag glyphs in
Segoe UI Emoji) and logged in the backlog — 7 tabs remain; emojis newer than the system font are
shown anyway, as boxes. The Direct2D layer stays open: the user asked for the pros and cons first.

### Iteration 3 — 2026-10-07

Second batch of answers: a click **inserts** the emoji into the previously focused window
(`SendInput`, Unicode) and the window **hides** (to the tray, minimized without it); the tab icons
are **monochrome** glyphs like Win+;. The Direct2D question stays open: the user asked what
`Vortice.Direct2D1` is for and what each option brings, which the first explanation did not answer.

### Iteration 4 — 2026-10-07

User request, logged in [TODO-FEATURES.md](TODO-FEATURES.md) (backlog, not this workfile):
Copy (UTF-8), Copy (PNG), and a user-chosen global shortcut that shows the window.

### Iteration 5 — 2026-10-07

Last answers: the Direct2D layer is the tray icon's hand-written interop — the tray icon's work
becomes a prerequisite; keyboard navigation goes to the backlog; no test project, everything is
checked by hand (Test Impact lists the manual checks). No open question remains.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | 5 | 2026-10-07 | Not applicable — no test project, checked by hand (Q&A #13) |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | How do the category tabs behave: one continuous list, or one page per tab? | One continuous list (like Win+;) | 2026-10-07 |
| 2 | Rendering: native Windows font, or bundled images? | No preference — compare during research | 2026-10-07 |
| 3 | Beyond the tabs, what is in scope: Recents tab, skin tones, click action, search box? | Click action only; log every proposed option in `workfiles/TODO-FEATURES.md` | 2026-10-07 |
| 4 | Is the subject straightforward or tricky? | Straightforward | 2026-10-07 |
| 5 | Data source: Emojibase or `emoji-test.txt`? | Emojibase | 2026-10-07 |
| 6 | Direct2D layer: tray icon's interop or Vortice? | Explain the pros and cons of each first | 2026-10-07 |
| 7 | Flags: images, letter pairs, or no Flags tab? | No Flags tab | 2026-10-07 |
| 8 | Emojis newer than the system font: hide or show? | Show them anyway | 2026-10-07 |
| 9 | Click action: clipboard, insert, or both? | Insert | 2026-10-07 |
| 10 | After a click: window stays or hides? | Hides | 2026-10-07 |
| 11 | Tab icons: color emojis or monochrome icons? | Monochrome (Win+;) | 2026-10-07 |
| 12 | Keyboard navigation: in scope or backlog? | Backlog | 2026-10-07 |
| 13 | Unit tests: xUnit project or by hand? | By hand | 2026-10-07 |
| 14 | Direct2D layer, after the pros and cons: tray icon's interop or Vortice? | The explanation does not say what Vortice is for nor the advantages of each situation | 2026-10-07 |
| 15 | Direct2D layer, after explaining what Vortice is: tray icon's interop or Vortice? | The tray icon's interop | 2026-10-07 |

---

*Last updated: 2026-10-07*
