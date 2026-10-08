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

Since the app then runs from sign-in, launching the exe by hand would start a second one: the app
becomes a **single instance** — a second launch of the same exe shows the first one's window, then
exits.

Backlog: the row **Start with Windows** of `TODO-FEATURES.md` (raised in
[20261007-global-hotkey.md](20261007-global-hotkey.md)).

Components touched: `Program.Main` (a new argument, the single instance), `UI/MainForm.cs` (the
menu item, the hidden start, the restart, the show asked by a second launch), a new
`Data/StartupShortcut.cs` (the shortcut and the Task Manager's state) and its COM interop, a new
`SingleInstance.cs` (the mutex and the signal).

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
- **The shortcut is the setting**: nothing goes into `settings.json`. The item's state is read from
  the Startup folder (and the Task Manager's state, below) each time the menu opens, so a shortcut
  the user deletes by hand unchecks it.
- **Checked** only while all three hold:
  1. `Emoji Selector.lnk` exists in the Startup folder;
  2. its target is **this exe** — the full path compared case-insensitively with
     `Environment.ProcessPath`. A shortcut to another exe (the app moved, a copy built elsewhere, a
     worktree) reads **unchecked**;
  3. it is **not disabled in the Task Manager**: *Startup apps* → *Disable* keeps the shortcut and
     writes the value `Emoji Selector.lnk` under
     `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder`,
     a binary whose first byte is **odd** while disabled (`03`), even while enabled (`02`). An odd
     first byte reads **unchecked**; a missing value or an unreadable key, enabled.
- **Never touched unasked**: no check, no repair, no rewrite at launch — only a click on the item
  writes or deletes the shortcut.

## Settings Menu Item

- **`Start with Windows`**, in the settings menu, after `Highlight color…`, before the separator of
  `Check for emoji updates…`.
- **Off by default**: no shortcut until the user checks it.
- Unchecked → a click **writes** the shortcut, pointing to this exe — replacing one that points
  elsewhere — and **deletes the Task Manager's value**, so Windows runs it again. Checked → a click
  **deletes** the shortcut and that value. No confirmation: nothing is lost.
- A Startup folder that cannot be written (or a shortcut that cannot be deleted) → a warning,
  `Could not change the startup shortcut: <reason>`, the item left as it was.
- The window stays open after the click, like the other toggles.

## Hidden Start — `--background`

- **`--background`** (`MainForm.BackgroundArgument`), parsed in `Program.Main` with `--title`: the
  app starts **without showing its window** — the tray icon, the shortcut hook, the pre-render and
  the data loading start as on a normal launch; the window's handle is not created until the first
  show (`MainForm.SetVisibleCore` skips `Application.Run`'s show).
- The window's **first show** — Win+;, Win+. or the tray icon — runs what a normal launch's show
  runs: sizing (`OnLoad`), centring, then Win+;'s placement under the text cursor — placed again at
  the end of `OnLoad` (`MainForm.showAnchor`), since the centring moved it after the first placement.
- Given twice, or with other arguments: the same as once; anything else stays ignored (RULES.md
  § Command-Line Arguments).
- **Restart after an emoji update** (`Restart now to use it?` → Yes): the user is looking at the
  window, so the restarted app is **shown** — `--title` kept, `--background` dropped.
  `Application.Restart` reuses the arguments as they are: the restart starts the exe itself with the
  filtered arguments, then exits — **after releasing the single instance** (below), or the new
  process would find the old one and exit (`MainForm.Restart`). The exe failing to start → a warning,
  `Could not restart the app: <reason>`; the app keeps running, without the single instance, the new
  data used at the next launch.

## Single Instance

- **One instance per exe**: a named mutex, `Local\EmojiSelector-<key>` (`Local\`: per Windows
  session), the key a hash of the exe's full path, upper-cased (a mutex name takes no `\`). Two
  builds in two folders — a worktree's next to `main`'s — still run side by side, as the agents'
  parallel launches need (CLAUDE.md § Launch); the same exe twice does not.
- Taken in `Program.Main`, before anything is loaded, kept until the app ends.
- **A second launch** of the same exe finds the mutex taken:
  - without `--background` → it **signals** the first one, then exits: the first one's window is
    shown and brought to the front, as the tray icon's click does it when hidden or covered — never
    hidden, a launch is never a toggle; already in front → it stays;
  - with `--background` (a sign-in while the app is already running) → it exits **silently**;
  - its other arguments, `--title` included, are ignored: the first instance keeps its own.
- **The signal**: a named auto-reset event, `Local\EmojiSelector-<key>-show`, waited on by a
  background thread of the first instance, which posts the show to the UI thread. Before setting
  it, the second instance calls `AllowSetForegroundWindow(ASFW_ANY)`: it is the app the user just
  launched, the one allowed to hand the foreground over.
- A mutex or an event that cannot be created (an unexpected Win32 error) → the app runs as before,
  without the single instance — never an error. A later launch finding no event exits all the same.
- An agent's check instance of an exe holds the single instance: it is ended before the delivery
  launch of the same exe, which would only show its window.

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
| A shortcut to another exe reads unchecked; a click repoints it to this exe | — (by hand) | — |
| Disabled in the Task Manager, the item reads unchecked; a click enables it again | — (by hand) | — |
| The exe launched again shows the first instance's window and exits; with `--background`, exits silently | — (by hand) | — |
| Two builds in two folders still run side by side | — (by hand) | — |

---

## Open Questions

- [x] ~~A shortcut pointing to **another exe** (the app moved, a copy built elsewhere — a worktree):
  the item checked anyway, or unchecked with a click that repoints it to this exe?~~ → Unchecked; a
  click repoints it to this exe
- [x] ~~Disabled in the **Task Manager's Startup apps** (the shortcut stays, Windows skips it): the
  item reads that state, or only the shortcut's presence?~~ → Read: disabled reads unchecked, a
  click enables it again
- [x] ~~**A second instance**: at sign-in the app runs; launching the exe again starts a second one
  (two tray icons, two hooks) — in this scope, or a backlog row?~~ → In this scope: single instance
  per exe, a second launch shows the first one's window
- [x] ~~The backlog row **Start with Windows** marked with this workfile?~~ → Yes

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

### Iteration 2 — 2026-10-08

Answers to the open questions: a shortcut to another exe or disabled in the Task Manager reads
unchecked, a click repointing or re-enabling it (*Mechanism* — the three conditions of the check
mark); the **single instance** joins the scope — designed as one per exe path, so builds in two
folders still run side by side, a second launch showing the first window (or exiting silently with
`--background`), the restart releasing it first (*Single Instance*). The backlog row is marked with
this workfile.

### Iteration 3 — 2026-10-08 — ✅ Implemented

Go given: code, unit tests and documentation, in a worktree (`.claude/worktrees/start-with-windows`,
branch `feature/start-with-windows`). The scope is frozen as the sections above stand.

### Iteration 4 — 2026-10-08 — 🧭 Implementation choices

No project rule broken. Choices the frozen design did not state:

- **Failure messages carry the reason**: `Could not change the startup shortcut: <reason>`, like the
  other warnings of the settings menu (`Could not update the emoji data: {0}`).
- **The restart failing to start the exe** (not in the design): a warning `Could not restart the app:
  <reason>`, the app kept running without the single instance, already released.
- **No handle at a hidden start**: `SetVisibleCore` skips the show without creating the window's
  handle — nothing needs it before the first show; the hook and the single instance post through the
  UI thread's synchronization context. The first show by Win+; places the window again at the end of
  `OnLoad` (`showAnchor`), the centring having moved it.
- **`StartupShortcut.Enable` takes the arguments** (`--background` passed by `MainForm`): `Data/` does
  not reference `UI/`.
- **`SingleInstance.cs` at the project root**, next to `Program.cs`: it belongs to the process, not to
  a folder's domain.
- **Glossary**: *Startup shortcut* (*raccourci de démarrage*) added — *Shortcut* already names Win+;,
  and the docs needed a distinct word for the `.lnk`. Not in the Implementation Log's planned steps.
- **RULES.md agent notes**: the check instance holding the single instance is ended before the
  delivery launch; the startup shortcut is checked by reflection, since the settings button cannot be
  clicked from a script.
- **Commits**: the shortcut classes, then `SingleInstance`, then the wiring in `Program` and `MainForm`
  in one commit — the constructor's new signature carries the single instance and the hidden start
  together.

Checked by hand, from scripts:

| Check | Result |
|---|---|
| `--background`: process alive, no window | ✅ |
| First Win+. after it: shown, foreground, sized as a normal launch (866 × 736), under the text cursor | ✅ |
| Launched again with `--background`: exits (code 0), the first instance unchanged | ✅ |
| Launched again by hand: exits (code 0), the first instance's window shown | ✅ — not brought to the front from the script: its launcher, a background shell, has no foreground right to hand over. To check with a double-click in the File Explorer |
| One instance of the exe after those launches; a copy in another folder runs side by side | ✅ |
| `StartupShortcut` by reflection: written (target, `--background`, folder, icon), checked; disabled in the Task Manager → unchecked; re-enabled by `Enable`, its value removed; a shortcut to another exe → unchecked, repointed by `Enable`; `Disable` deletes the shortcut and the value, twice without error | ✅ — the Startup folder and the registry left clean |
| Win+. hiding the window once shown | ⚠️ Not conclusive: the Claude window comes back above the app's while the checks run, and another session's check instance answers Win+. too — `main`'s build behaves the same in the same conditions |
| Restart after an emoji update | Not checked: it needs a newer Emojibase version online |

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 4 | 2026-10-08 | `Data/ShellLinkInterop.cs`, `Data/StartupShortcut.cs`, `SingleInstance.cs`, `Program.cs`, `UI/MainForm.cs` — three commits |
| Unit tests | 4 | 2026-10-08 | Not applicable: no test project. Checked by hand from scripts, see Iteration 4 |
| RULES.md | 4 | 2026-10-08 | § Command-Line Arguments, § Window and Tray Icon, § Emoji Data (the restart), a § Start with Windows with its § Single Instance |
| README (English and French) | 4 | 2026-10-08 | A *Start with Windows* bullet, the gear's menu sentence |
| Glossary (English and French) | 4 | 2026-10-08 | *Startup shortcut* — added: *Shortcut* already means Win+; |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which Windows mechanism launches the app at sign-in? | A shortcut in the Startup folder | 2026-10-08 |
| 2 | Where is the option, and its default? | The settings menu ⚙, off by default | 2026-10-08 |
| 3 | Launched at sign-in, does the app show its window? | No: hidden in the tray | 2026-10-08 |
| 4 | Straightforward or tricky subject? | Straightforward: a single scout pass | 2026-10-08 |
| 5 | A shortcut pointing to another exe: checked, or repointed by a click? | Unchecked; a click repoints it to this exe | 2026-10-08 |
| 6 | Disabled in the Task Manager: read by the item? | Yes: disabled reads unchecked, a click enables it again | 2026-10-08 |
| 7 | A second instance: in scope, or a backlog row? | In this scope | 2026-10-08 |
| 8 | Mark the backlog row with this workfile? | Yes | 2026-10-08 |

---

*Last updated: 2026-10-08*
