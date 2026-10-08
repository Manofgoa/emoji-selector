# Settings Menu Sections

> Working document — the settings menu ⚙ reorganized into sections, one per feature, separated by
> lines.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The settings menu ⚙ (`MainForm.CreateSettingsMenu`) has grown one item per workfile, in the order
they were delivered: nine items in a single list, one separator before the last one.

```
Open app folder
New group…
Show groups ▸
Reset window size
Show frequently used
Clear frequently used
Show French names
Highlight color…
─────────────
Check for emoji updates…
```

This workfile groups them into **sections**, one per feature, **separated by lines only** — no
section title (Q&A #1). The items keep their labels (Q&A #3) and their behaviour: their order and
the separators change, and a colour item shows its colour (see *Colour Items* below, Iteration 2).

| In scope | Out of scope |
|---|---|
| The order of the settings menu's items, the separators between sections | Any item's label, behaviour, enabled or checked state |
| `Highlight color…` showing its colour as a swatch | The other menus: an emoji's right-click menu, a section's "…" menu, the tray icon's menu |
| A shared rule: a colour item shows its colour, in every mini-app | New items |
| `RULES.md`, `README.md` / `README.fr.md` following the new order | Bringing image-grid-fusion in line — it already follows the rule |

---

## Sections

Five sections, in the order the features appear in the window — the frequent tab, the custom tab,
the details panel at the bottom — then the window itself, then the app (Q&A #2). A plain
`ToolStripSeparator` between two sections, none before the first nor after the last.

| # | Section | Items, in order |
|---|---|---|
| 1 | Frequently used | `Show frequently used`, `Clear frequently used` |
| 2 | Custom groups | `New group…`, `Show groups ▸` |
| 3 | Details panel | `Show French names`, `Highlight color…` |
| 4 | Window | `Reset window size` |
| 5 | App | `Open app folder`, `Check for emoji updates…` |

```
✓ Show frequently used
  Clear frequently used
─────────────
  New group…
  Show groups          ▸
─────────────
✓ Show French names
  Highlight color…
─────────────
  Reset window size
─────────────
  Open app folder
  Check for emoji updates…
```

- `Check for emoji updates…` is **no longer** "last after a separator" on its own: it is the last
  item of the *App* section, after `Open app folder`. Still the last item of the menu.
- The sections have **no title** in the menu: their names above are the design's, used in the code's
  comments and in the docs.
- Every item keeps its handler and its `Opening` refresh (checked states, *Clear frequently used*
  and *Show groups* greyed) — only the order of the `menu.Items.Add` calls and the separators move.

---

## Colour Items

Requested by the user (Iteration 2): a menu item choosing a colour **shows that colour**, like the
⚙ menu's *Border color…* and *Arrow color…* in image-grid-fusion (`MainForm.SetSwatch`).

- `Highlight color…` gets a **swatch** as its item image: a **rounded square** of the highlight
  colour in use, 16 logical pixels, corners rounded at 3 logical pixels, a 1-pixel
  `SystemColors.ControlDark` outline, drawn antialiased (Q&A #6) — closer to Windows 11 than
  image-grid-fusion's plain square.
- Drawn at the window's DPI (`LogicalToDeviceUnits`). Refreshed in the menu's **`Opening`**, with the
  checked states: it then always shows the colour in use and the current DPI, without an
  `OnDpiChanged` hook — the menu is never open while the colour changes nor across a DPI change. The
  previous bitmap disposed when replaced, the last one with the menu.
- The checked items keep their check mark: WinForms draws the check in the image margin **in place
  of** the image only on an item that has one; `Highlight color…` is never checked.

### Shared Rule

The same behaviour becomes a **rule shared by every mini-app**, in a **new file
`mini-apps/shared/RULES.md`** — the shared UI rules — imported by `mini-apps/CLAUDE.md` with
`@shared/RULES.md` (Q&A #5).

- The rule: a menu item that picks a colour shows **the colour in use** as its image — a swatch,
  outlined so a colour close to the menu's background still shows — drawn at the monitor's DPI and
  up to date whenever the menu opens.
- The rule does **not** impose the swatch's shape: image-grid-fusion's plain square already follows
  it, emoji-selector's rounded one too.
- `mini-apps/` and `shared/` are not git repositories: these two files are written, **not
  committed**.
- Changing `mini-apps/CLAUDE.md` (the import) is a rule change: the other **running** sessions of the
  workspace are told to re-read it, per the user's global instructions.

---

## Code

| Piece | Where |
|---|---|
| The items added section by section, a separator between two | `UI/MainForm.cs` — `CreateSettingsMenu` |
| The highlight colour's swatch, refreshed on `Opening` | `UI/MainForm.cs` — `CreateSettingsMenu`, a `SetSwatch` helper drawing the rounded square |

No new type, no new constant: the labels' constants are unchanged.

---

## Documentation

| File | Change |
|---|---|
| `RULES.md` § Window and Tray Icon | The settings button's rows in the menu's new order; the missing `Clear frequently used` row added in its place, so the table lists the whole menu; one bullet describing the five sections and the separators |
| `RULES.md` § Emoji Data | `Check for emoji updates…`: "last in the settings menu, in the app section after `Open app folder`" instead of "last … after a separator" |
| `RULES.md` § Details Panel | *Search highlight*: the `Highlight color…` item shows the colour in use |
| `README.md` § Window, `README.fr.md` same paragraph | The gear's items listed in the new order, grouped by section |
| `mini-apps/shared/RULES.md` (new) | The colour items rule (see *Shared Rule*) |
| `mini-apps/CLAUDE.md` | `@shared/RULES.md` imported, one line saying what the file holds |

---

## Test Impact

**No unit test**: the app has no test project, and the change is the order of a WinForms menu's
items — nothing testable outside the running app. Checked by hand at the delivery launch.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none (see above) | — | — |

---

## Open Questions

Every question the design cannot settle on its own, listed before Iteration 1 —
not only the blocking ones.

- [x] ~~Section form: titles, separators, submenus?~~ → Separators only (Q&A #1)
- [x] ~~Which sections, in which order?~~ → Five, in the window's order (Q&A #2)
- [x] ~~Labels shortened under a section?~~ → Kept as they are (Q&A #3)
- [x] ~~Exploration depth?~~ → Straightforward: one scout pass (Q&A #4)
- [x] ~~Where does the shared colour-items rule live: a section of `mini-apps/CLAUDE.md`, or a new file under `shared/` imported by it?~~ → A new `shared/RULES.md`, imported by `mini-apps/CLAUDE.md` (Q&A #5)
- [x] ~~The swatch's look: image-grid-fusion's (a 16-pixel square, grey outline), or another?~~ → A rounded square (Q&A #6)

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-08

Initial design from the scoping batch (Q&A #1–#4): five sections separated by lines, no title, in
the window's order (frequent, custom groups, details panel, window, app), labels unchanged. One
scout pass, done directly (a single question: where the menu and its description live): the menu is
built in `MainForm.CreateSettingsMenu` alone; it is described in `RULES.md` (the window table, the
emoji data section) and in the *Window* paragraph of `README.md` / `README.fr.md`; no test project.
The `RULES.md` table lacks a `Clear frequently used` row: added with the reorder, so the table lists
the whole menu.

### Iteration 2 — 2026-10-08

User request: the colour items of the menu show their colour, as in image-grid-fusion; and that
becomes a rule in `shared`. Added *Colour Items*: `Highlight color…` gets a swatch as its image,
refreshed on the menu's `Opening`; the shared rule; the docs following. Read in image-grid-fusion:
`MainForm.SetSwatch` (a 16-logical-pixel square, `ControlDark` outline), redrawn on `OnDpiChanged`
and after each pick. Two open questions: where the shared rule lives — `shared/` holds only the
plugin's skills today, the shared rules being `mini-apps/CLAUDE.md` — and the swatch's look.

### Iteration 3 — 2026-10-08

Answers to Q&A #5 and #6. The shared rule goes in a new `mini-apps/shared/RULES.md`, imported by
`mini-apps/CLAUDE.md`; it requires a swatch of the colour in use, not its shape, so image-grid-fusion
already follows it. The swatch is a rounded square (3 logical pixels of radius, antialiased). Noted:
`mini-apps/` and `shared/` are not git repositories — the two files are written, not committed.

### Iteration 4 — 2026-10-08 — ✅ Implemented

Go given: code, tests and documentation, in a worktree (`.claude/worktrees/settings-menu-sections`,
branch `feature/settings-menu-sections`). The scope is frozen as Iteration 3 left it.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable: no test project (see *Test Impact*) |
| README | | | |
| RULES | | | |
| Shared rule | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which form for the sections: titles + separators, separators only, or submenus? | Separators only | 2026-10-08 |
| 2 | Which sections, in which order: five by feature in the window's order, or three broad ones (Display / Content / App)? | Five sections, in the window's order | 2026-10-08 |
| 3 | Under a section, are the items' labels shortened? | Kept as they are | 2026-10-08 |
| 4 | Is the subject straightforward, or tricky / long? | Straightforward | 2026-10-08 |
| 5 | Where does the shared colour-items rule live? | A new file under `shared/` | 2026-10-08 |
| 6 | Which look for the swatch? | A rounded square | 2026-10-08 |

---

*Last updated: 2026-10-08*
