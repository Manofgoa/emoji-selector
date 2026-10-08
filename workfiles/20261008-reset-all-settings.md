# Reset All Settings

> Working document — a settings menu item putting the app back to its first-launch state: every
> setting at its default, every local file the app wrote deleted, after a confirmation, then a restart.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

**`Reset all settings…`**, in the settings menu ⚙, deletes everything the app wrote on disk and in
the registry — settings, counters, custom groups, startup shortcut, downloaded emoji data, render
cache — after a detailed Yes / No confirmation, then **restarts** the app, which comes back exactly
as on its very first launch.

Components touched:

| Component | Role |
|---|---|
| `UI/MainForm.cs` | The menu item (`CreateSettingsMenu`), the confirmation, the order of the steps, the restart (`Restart`, reused) |
| New `Data/AppReset.cs` | The list of what is deleted, and the deletion itself — each name taken from the constant of the code that owns it |
| `Data/StartupShortcut.cs` | `Disable` reused for the startup shortcut and the Task Manager's value |
| `Drawing/EmojiBitmapCache.cs` | The pre-render cancelled before `cache\` is deleted |

---

## Menu Item

- **Label**: `Reset all settings…` — the ellipsis because a dialog follows.
- **Place**: the settings menu's *app* section (`RULES.md` § Frame: a new item joins the section of
  its feature), **last**, after `Check for emoji updates…` — the most destructive item at the end,
  away from the everyday ones. *(Open question 3.)*
- Always enabled: even a fresh install has `settings.json`, `cache\` and `emoji-data\`.

---

## Confirmation

A `MessageBox`, **Yes / No**, **No the default** (`MessageBoxDefaultButton.Button2`), the warning
icon, listing what is lost:

```
Reset Emoji Selector to its first-launch state?

This deletes, and cannot be undone:
• the custom groups and their emojis
• the use counters of Frequently used
• the window size, the tray emoji, the details panel's settings
• Start with Windows
• the downloaded emoji data — back to version {embedded version}
• the emoji image cache

The app then restarts.
```

- The emoji data line names the **embedded** version (the one the app carries), so the user sees
  what an update they downloaded falls back to.
- No → nothing changed.

---

## What Is Deleted

Everything the app writes, and nothing else — **never** the exe's folder itself, nor a file the app
does not own.

| What | Where | Owner (name constant) |
|---|---|---|
| Settings — window size, tray emoji, `showFrequent`, `showFrench`, `highlightColor` | `settings.json` next to the exe, and its temporary sibling | `SettingsFile.FileName` |
| Use counters | `usage.json` next to the exe, and its temporary sibling | `EmojiUsage.FileName` |
| Custom groups | `custom-groups.json` next to the exe, and its temporary sibling | `CustomGroups.FileName` |
| Render cache | the whole `cache\` folder next to the exe | `EmojiBitmapCache.FolderName` |
| Emoji data | the whole `emoji-data\` folder next to the exe — recreated from the embedded copy at the restart | `EmojiDataFolder.FolderName` |
| Startup shortcut | `Emoji Selector.lnk` in the Startup folder, and the Task Manager's `StartupApproved` value | `StartupShortcut.Disable` *(Open question 1)* |

- **Each name comes from its owner's constant**, never a literal repeated in `AppReset`: a file
  renamed later is still deleted. The temporary siblings (`.tmp` / `.new`) are left behind by an
  interrupted write; they go too.
- A file or folder already missing is not an error.

---

## Order of the Steps

1. **Confirmation** — No → stop.
2. **Pre-render cancelled** (the grid's `EmojiBitmapCache`): its background thread would otherwise
   write `cache\` again right after the deletion. Cancelling does not wait for the thread: an atlas
   whose write had already started may still land — harmless, it is complete and valid, the next
   instance simply reuses it.
3. **Deletion** — every row of *What Is Deleted*, each one attempted even when another failed.
   *(Open question 2 for the failures.)*
4. **Restart** — `MainForm.Restart`, unchanged: the same arguments, `--title` included,
   `--background` left out, the single instance released first. No "Restart now?" question: the
   confirmation already said so. *(Open question 4 for a failed restart.)*

- Nothing is written between step 3 and the exit: the app writes its files only on a user action,
  and nothing at exit (`OnFormClosing` writes nothing).
- The restarted instance finds no file: default size, centred, 🙂 tray icon, frequent tab shown,
  French row shown, yellow highlight, no group, no counter, `emoji-data\` written from the embedded
  copy, every emoji pre-rendered again.

---

## Documentation

- `RULES.md` § Window and Tray Icon: one row for the item; § Frame: the *app* section lists it.
- `README.md` / `README.fr.md`: the item in the settings menu's description.
- `GLOSSARY.md` / `GLOSSARY.fr.md`: no new term.

---

## Test Impact

There is **no test project** in this repository (CONTRIBUTING.md § Build: *check a change by hand
in the running app*): nothing is created nor updated. Checked by hand — on a **copy** of the build
folder, never the user's own, since the reset deletes its files:

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| No in the confirmation (and Esc, Enter by default) changes nothing | — (by hand) | — |
| Yes deletes `settings.json`, `usage.json`, `custom-groups.json`, `cache\`, `emoji-data\` and their temporary siblings | — (by hand) | — |
| Yes deletes the startup shortcut and the `StartupApproved` value | — (by hand) | — |
| The app restarts shown, `--title` kept, every setting at its default, `emoji-data\` back to the embedded version | — (by hand) | — |
| A missing file or folder is not an error | — (by hand) | — |
| A file that cannot be deleted (held open by another process) follows Open question 2 | — (by hand) | — |

The settings button cannot be clicked from a script (custom-drawn, no accessibility): the deletion
is checked by calling `AppReset` by reflection on the built dll of the copy, the dialog and the
restart by hand.

---

## Open Questions

- [ ] **1. Startup shortcut of another exe.** `Emoji Selector.lnk` has one name for every copy of
  the app: a reset from a worktree build would delete the shortcut that points to `main`'s exe.
  Proposal: delete it **only when it targets this exe** (as `IsEnabled` reads it); a shortcut to
  another exe is left alone.
- [ ] **2. A deletion that fails** (a file held open by another process, a read-only folder).
  Proposal: every deletion attempted; any failure → a warning `Some files could not be deleted:` with
  their names, then **restart anyway** — what was deleted is reset, the rest keeps its value.
- [ ] **3. Place in the menu.** Proposal: last of the *app* section, after `Check for emoji
  updates…`. Alternative: a sixth section of its own, at the very end, alone under a line.
- [ ] **4. A restart that fails** (`Process.Start` refused). The running app still holds the old
  settings in memory, and would write them back on the next action. Proposal: a warning `The settings
  were reset. Start the app again to finish.`, then the app **exits** rather than keeps running.

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-08

Initial design, from the scoping batch (Q&A 1–4): everything the app wrote is deleted — settings,
counters, custom groups, startup shortcut, emoji data, render cache —, after a Yes / No confirmation
listing what is lost, No the default; then the app restarts through `MainForm.Restart`. Exploration
(single pass, the subject being straightforward): the app writes its files on user actions only,
nothing at exit; the pre-render thread is the only writer that may run on its own, so it is
cancelled first. Four open questions: another exe's startup shortcut, failed deletions, menu place,
failed restart.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project — checked by hand (see *Test Impact*) |
| RULES.md | | | |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Beyond `settings.json` and `cache\`, what does the reset delete too? (counters, custom groups, Start with Windows, emoji data) | All four | 2026-10-08 |
| 2 | Once the files are deleted, what does the app do? (restart / apply live / exit) | Restart | 2026-10-08 |
| 3 | Which confirmation before resetting? (detailed Yes / No / short Yes / No / typed word) | Detailed Yes / No, No the default | 2026-10-08 |
| 4 | Is the subject straightforward or tricky / long to explore? | Straightforward | 2026-10-08 |
| 5 | Open questions 1–4 | | 2026-10-08 |

---

*Last updated: 2026-10-08*
