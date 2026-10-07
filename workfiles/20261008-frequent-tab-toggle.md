# Frequent Tab Toggle

> Working document — a setting turning the frequent tab on or off, from the settings menu and from a
> "…" button on the frequent section's header.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

The **frequent tab** ([20261008-frequent-tab.md](20261008-frequent-tab.md)) is always the first tab
today. This workfile lets the user **hide** it: a checkable item in the settings menu ⚙, and a
**"…" button** at the right end of the *Frequently used* section's header whose menu hides it too —
the same setting, two places.

- **Hidden**: the star tab leaves the tab strip and its section leaves the grid. The use counters
  **keep counting** in `usage.json` (Q&A #1): shown again, the section is up to date.
- **Saved**: the setting lives in a new **`settings.json`** next to the exe, meant to hold the app's
  future settings too (Q&A #3). Shown by default.
- **After the custom tabs** (Q&A #10): this workfile is implemented once
  [20261008-custom-tabs.md](20261008-custom-tabs.md) is delivered. It **reuses** that workfile's "…"
  button on section headers, and **applies the same hiding logic to the custom groups**.

| In scope | Out of scope |
|---|---|
| The setting, its `settings.json` file | Any other setting |
| The checkable item in the settings menu ⚙ | The tray icon's menu (Q&A #2) |
| A *Hide* item in the "…" menu of the frequent section — the button itself comes from the custom tabs | A "…" button on the catalog's sections |
| Hiding / showing the tab and its section while the app runs | Changing how the counters work |
| The same hiding for the custom groups (see *Custom Groups*) | |

### Existing code

| Fact | Where |
|---|---|
| The tab list is built once, frequent first: `[CreateFrequentCategory(), .. categories]`, then given to the grid and the strip | `UI/MainForm.cs:88-90` |
| The grid copies the list into a fixed array; `ReplaceCategory(0, …)` swaps the frequent section in place after a use or *Clear* | `UI/EmojiGrid.cs:68-71`, `231-239`; `UI/MainForm.cs:305`, `392` |
| The strip keeps the list it was given; a tab's index is its section's index (`TabClicked`, `ActiveTab`, `ActiveCategory`) | `UI/CategoryTabStrip.cs:45`, `58-60`, `117` |
| The window's minimum width comes from the strip's tab count | `UI/MainForm.cs:93`, `UI/CategoryTabStrip.cs:86-87` |
| Section headers are drawn as plain text, the width of the grid, ending in an ellipsis | `UI/EmojiGrid.cs:263-271` |
| The settings menu is a `ContextMenuStrip` owned by `MainForm`: *Open app folder*, *Clear frequently used* (greyed while no counter) | `UI/MainForm.cs:286-295` |
| `usage.json`: read at launch, missing or invalid → empty; written through `.tmp` then a replace; a read-only folder is not an error | `Data/EmojiUsage.cs` |
| The **custom tabs** workfile (in design) plans a "…" button on its group headers — and states that the catalog and frequent sections have none — and a mutable list of sections | [20261008-custom-tabs.md](20261008-custom-tabs.md) |

---

## Setting and File

- One setting, **show the frequent tab**, on by default.
- Saved in **`settings.json`** next to the exe (`AppContext.BaseDirectory`), **not** in `cache\` —
  the same reasoning as `usage.json`: that folder is disposable. A JSON object,
  `{ "showFrequent": true }`, ready for more keys.
- A new `Data/AppSettings.cs` owns it, on `EmojiUsage`'s pattern: read once at launch — missing,
  invalid, or a key missing → the default; written after each change, through `settings.json.tmp`
  then a replace; a folder that cannot be written is not an error, the setting lives in memory until
  the app ends.

---

## Settings Menu

- A **checkable** item, *Show frequently used*, checked while the tab is shown. A click flips it.
- *Clear frequently used* stays where it is and **stays usable while the tab is hidden**: the
  counters still count.

---

## "…" Button on the Frequent Section

- The **same "…" button** as the custom groups' headers ([20261008-custom-tabs.md](20261008-custom-tabs.md),
  *Group Menu*): its look, its place at the right end of the header, the header text shortened before
  it. This workfile gives it to the frequent section too.
- Its menu holds **Hide frequently used** — the same setting as the settings menu's item, turned off.
- Never in search mode (the frequent section is not shown then).
- Showing the tab again is done from the settings menu only: the "…" button leaves with its section.

---

## Custom Groups

The same logic applied to the custom groups of the custom tab (Q&A #10). Its exact shape is open
(see *Open Questions*): what is hidden — each group, or the custom tab as a whole — and where it is
shown again from.

---

## Hiding and Showing

- **Hidden**: the grid and the strip get the catalog's categories alone; the first tab is *Smileys &
  People*. A use still records the counter (`MainForm.OnEmojiUsed`) but replaces no section.
- **Shown again**: the frequent section is rebuilt from the counters and put back first.
- The grid and the strip need **an entry point to replace their list of categories** — today both
  take it once, at construction. Indices shift by one: the active tab and the selection are reset.
- **Every show** of the window still scrolls the grid to the top — the first section, whatever it is.

---

## Documentation

| File | Change |
|---|---|
| `RULES.md` | *Frequent Tab*: the setting, `settings.json`, the "…" button, hidden → still counted; *Window and Tray Icon*: the settings menu's new item |
| `README.md` / `README.fr.md` | The setting, where it is |
| `GLOSSARY.md` / `GLOSSARY.fr.md` | No new term planned |

---

## Test Impact

No test project exists, and no earlier workfile created one: the behaviours below are **checked by
hand** in the running app, not by unit tests. `AppSettings` can be checked by a throwaway console
harness in the session's scratchpad, as `EmojiUsage` was.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| `settings.json` missing, invalid or without the key → the tab is shown | — (manual / scratchpad harness) | — |
| Unchecking *Show frequently used* removes the tab and the section; checking it puts them back first | — (manual) | — |
| *Hide frequently used* in the "…" menu does the same as unchecking, and the ⚙ item reflects it | — (manual) | — |
| Hidden: a use still adds 1 to the counter; shown again, the section shows it | — (manual) | — |
| The setting survives a restart; a read-only folder → no error, kept until the app ends | — (manual) | — |
| A hidden custom group behaves like the hidden frequent tab (shape pending the open questions) | — (manual) | — |

---

## Open Questions

- [ ] Labels: *Show frequently used* (checkable, ⚙) and *Hide frequently used* (the "…" menu)?
- [ ] Does hiding ask for a confirmation? (Proposal: no — nothing is lost, the counters keep counting.)
- [x] ~~Is the "…" button always drawn, or only while the mouse is over the header?~~ → As the custom groups' button: this workfile reuses it (Q&A #10)
- [ ] The window's minimum width while the tab is hidden: kept as with every tab (toggling never resizes the window), or one tab narrower?
- [ ] Where do the grid and the selection go after a toggle while the window is open: back to the top on the first emoji, or the first emoji in view?
- [x] ~~The custom tabs workfile also designs a "…" button on its group headers and says the frequent section has none: implement the "…" mechanism here first, and update that workfile so its groups reuse it?~~ → No: wait for the custom tabs to be delivered, reuse their "…" button, and apply the same hiding logic to the custom groups (Q&A #10)
- [ ] Custom groups: what is hidden — **each group** on its own (its "…" menu gets *Hide*), or the **custom tab as a whole** (one setting, like the frequent tab)?
- [ ] Custom groups: where is a hidden one shown again from — one checkable item per group in the settings menu, a submenu, or something else?
- [ ] Does a hidden custom group still receive emojis from the right-click *Add to…* menu?

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-08

Initial design. Scoping answers (Q&A #1-4): hidden → the tab and its section leave, the counters
keep counting; the setting is a checkable item in the settings menu ⚙; it is saved in a new
`settings.json` next to the exe, shown by default; the subject is expected to be straightforward
(one scout pass, done directly: the questions chained).

While the exploration ran, the user added a second place for the setting: a **"…" button** at the
end of the frequent section's header, whose menu hides the group — the same setting as the settings
menu's item.

The exploration found that the grid and the tab strip take the category list once, at construction,
with the tab ↔ section mapping by index: hiding needs an entry point replacing that list. The custom
tabs workfile, in design, needs the same and plans "…" buttons of its own.

### Iteration 2 — 2026-10-08

The user decided the order (Q&A #10): this workfile waits for the custom tabs
([20261008-custom-tabs.md](20261008-custom-tabs.md)) to be delivered, reuses their "…" button for
the frequent section, and applies the same hiding logic to the custom groups. The "…" button's look
is no longer designed here; new open questions on the custom groups' hiding.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project: manual checks (see *Test Impact*) |
| README | | | |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Frequent group turned off: what happens to the tab and the counters? | Hidden, the counters keep counting | 2026-10-08 |
| 2 | Where is the setting? | The settings menu ⚙, a checkable item | 2026-10-08 |
| 3 | Is the setting saved between launches? | Yes, in `settings.json`, on by default | 2026-10-08 |
| 4 | Straightforward or tricky / long subject? | Straightforward | 2026-10-08 |
| 5 | Labels: *Show frequently used* (⚙) and *Hide frequently used* ("…")? | | 2026-10-08 |
| 6 | Does hiding ask for a confirmation? | | 2026-10-08 |
| 7 | "…" button always drawn, or on hover only? | Moot: the custom groups' button is reused (#10) | 2026-10-08 |
| 8 | Minimum window width while hidden? | | 2026-10-08 |
| 9 | Grid and selection after a toggle with the window open? | | 2026-10-08 |
| 10 | Implement the "…" mechanism here first, and update the custom tabs workfile? | Unasked — the user said: wait for the custom tabs, and apply the same logic to them | 2026-10-08 |
| 11 | Custom groups: hide each group, or the custom tab as a whole? | | 2026-10-08 |
| 12 | Custom groups: where is a hidden one shown again from? | | 2026-10-08 |
| 13 | Does a hidden custom group still receive emojis from *Add to…*? | | 2026-10-08 |

---

*Last updated: 2026-10-08*
