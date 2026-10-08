# Start with Windows

> Working document — an option of the settings menu launching the app at sign-in, hidden in the tray.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Win+; and Win+. reach the app only while it runs (RULES.md § Shortcut): a user who forgets to
launch it gets Windows' panel. This workfile adds **`Start with Windows`**, a checked item of the
settings menu ⚙: while checked, Windows launches the app at sign-in, straight into the notification
area, its window hidden until Win+; or the tray icon shows it.

Backlog: the row **Start with Windows** of `TODO-FEATURES.md` (raised in
[20261007-global-hotkey.md](20261007-global-hotkey.md)).

Components touched: `Program.Main` (a new argument), `UI/MainForm.cs` (the menu item, the hidden
start, the restart), a new `Data/StartupShortcut.cs` (the shortcut) and its COM interop.

---

## Mechanism — a Shortcut in the Startup Folder

- A **`.lnk` shortcut** in the user's **Startup folder** (`Environment.SpecialFolder.Startup`,
  `shell:startup`) — no administrator rights, no registry key, visible in the File Explorer and in
  the Task Manager's *Startup apps*.
- Named **`Emoji Selector.lnk`** (`StartupShortcut.FileName`, one constant).
- Its target is the **running exe** (`Environment.ProcessPath`), its arguments **`--background`**,
  its working folder the exe's folder, its icon the exe's.
- Written and read through **`IShellLinkW` + `IPersistFile`**, declared by hand like Direct2D
  (`Drawing/Direct2DInterop.cs`): no new dependency.
- **The shortcut is the setting**: nothing goes into `settings.json`. The item's check mark is read
  from the Startup folder each time the menu opens, so a shortcut the user deletes by hand unchecks
  it.
- **Never touched unasked**: no check, no repair, no rewrite at launch — only a click on the item
  writes or deletes the shortcut.

## Settings Menu Item

- **`Start with Windows`**, in the settings menu, after `Highlight color…`, before the separator of
  `Check for emoji updates…`.
- **Off by default**: no shortcut until the user checks it.
- Unchecked → a click **creates** the shortcut; checked → a click **deletes** it. No confirmation:
  nothing is lost.
- A Startup folder that cannot be written (or a shortcut that cannot be deleted) → a warning,
  `Could not change the startup shortcut`, the item left as it was.
- The window stays open after the click, like the other toggles.

## Hidden Start — `--background`

- **`--background`** (`MainForm.BackgroundArgument`), parsed in `Program.Main` with `--title`: the
  app starts **without showing its window** — the tray icon, the shortcut hook, the pre-render and
  the data loading start as on a normal launch.
- The window's **first show** — Win+;, Win+. or the tray icon — runs what a normal launch's show
  runs: sizing (`OnLoad`), centring, then Win+;'s placement under the text cursor.
- Given twice, or with other arguments: the same as once; anything else stays ignored (RULES.md
  § Command-Line Arguments).
- **Restart after an emoji update** (`Restart now to use it?` → Yes): the user is looking at the
  window, so the restarted app is **shown** — `--title` kept, `--background` dropped.
  `Application.Restart` reuses the arguments as they are: the restart starts the exe itself with the
  filtered arguments, then exits.

---

## Test Impact

There is **no test project** in this repository (CONTRIBUTING.md § Build: *check a change by hand
in the running app*): nothing is created nor updated. Checked by hand:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| Checking the item creates `Emoji Selector.lnk` in `shell:startup`, target the exe, arguments `--background` | — (by hand) | — |
| Unchecking it deletes the shortcut; deleted by hand, the item reads unchecked | — (by hand) | — |
| `--background` starts with no window, the tray icon there; Win+; then shows it under the cursor, sized | — (by hand) | — |
| The restart after an emoji update comes back shown, `--title` kept | — (by hand) | — |

---

## Open Questions

- [ ] A shortcut pointing to **another exe** (the app moved, a copy built elsewhere — a worktree):
  the item checked anyway, or unchecked with a click that repoints it to this exe?
- [ ] Disabled in the **Task Manager's Startup apps** (the shortcut stays, Windows skips it): the
  item reads that state, or only the shortcut's presence?
- [ ] **A second instance**: at sign-in the app runs; launching the exe again starts a second one
  (two tray icons, two hooks) — in this scope, or a backlog row?
- [ ] The backlog row **Start with Windows** marked with this workfile?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-08

Initial design from the scoping batch: a shortcut in the Startup folder (the user's choice over the
`Run` registry key and a scheduled task), a `Start with Windows` item of the settings menu, off by
default, and a hidden start through `--background`. Proposed on top: the shortcut as the only
state (nothing in `settings.json`), never touched unasked, and a restart after an emoji update that
comes back shown.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | Not applicable: no test project |
| RULES.md | | | § Command-Line Arguments, § Window and Tray Icon |
| README (English and French) | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which Windows mechanism launches the app at sign-in? | A shortcut in the Startup folder | 2026-10-08 |
| 2 | Where is the option, and its default? | The settings menu ⚙, off by default | 2026-10-08 |
| 3 | Launched at sign-in, does the app show its window? | No: hidden in the tray | 2026-10-08 |
| 4 | Straightforward or tricky subject? | Straightforward: a single scout pass | 2026-10-08 |
| 5 | A shortcut pointing to another exe: checked, or repointed by a click? | | |
| 6 | Disabled in the Task Manager: read by the item? | | |
| 7 | A second instance: in scope, or a backlog row? | | |
| 8 | Mark the backlog row with this workfile? | | |

---

*Last updated: 2026-10-08*
