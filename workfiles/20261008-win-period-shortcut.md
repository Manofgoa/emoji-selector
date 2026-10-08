# Win+. Shortcut

> Working document — Win+. caught too, like Win+;, so the "Emoji — Windows+Period" entry of an app's
> menu opens the app.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the app takes **Win+;** only (`Input/ShortcutHook.cs`); RULES.md § Shortcut says *Win+. is never
touched*, and the README says Win+. always opens Windows' panel. The user wants **Win+.** replaced
too, for one reason in particular: an app's context menu offering **Emoji — Windows+Point** (Edge,
in the user's capture) does nothing while the app runs.

Agreed in the scoping pass (Q&A 1–4):

- Win+. does **exactly what Win+; does**: hidden or covered → shown under the text cursor and
  brought to the front; already in front → hidden, the previous window getting the foreground back.
- Every app whose menu has that entry is wanted, whatever the way it opens the panel — within what
  a keyboard hook can see (see *Menus*).
- A straightforward subject: one scout pass.

---

## How the Menus Open the Panel

Research (Chromium source, `ui/base/emoji/emoji_panel_helper_win.cc`):

- **Chromium** — Chrome, Edge, and Electron apps through `app.showEmojiPanel()` — opens the panel by
  **injecting Win+Period with `SendInput`**: `VK_LWIN` down, `VK_OEM_PERIOD` down, `VK_LWIN` up,
  `VK_OEM_PERIOD` up, in one call; no Shift, no `dwExtraInfo`. A low-level hook sees these keys like
  real ones. High confidence.
- **`CoreInputView.TryShow(CoreInputViewKind.Emoji)`** (WinRT, Windows 10 1809+) opens the panel
  through an API: no key event exists, **no keyboard hook can catch it**. Which apps use it (XAML /
  WinUI text boxes, maybe Firefox) is not sourced.

### What the Hook Does With It Today

- The `;` key is the one typing `;` in the foreground window's layout (`VkKeyScanExW`), Shift exactly
  as that layout needs it, Ctrl and Alt up (`ShortcutHook.IsSemicolonKey`). Injected keys are
  processed — only the app's own dummy key (`InjectedMarker`) is skipped.
- On **AZERTY** (the user's main layout, `040C`), `;` is the `; .` key **unshifted**, i.e.
  `VK_OEM_PERIOD`: Chromium's injected Win+Period **already matches** Win+;. On **QWERTY** (the user's
  second layout, `0409`), `;` is `VK_OEM_1`: Chromium's Win+Period is not caught, and Windows' panel
  opens.
- **Checked from a script** (design session, AZERTY foreground, the main checkout's Debug build):
  Chromium's exact four-key sequence showed the hidden window and brought a covered one to the
  front. So the same keys do work when a script sends them. Why the Edge menu of the capture did
  nothing was not pursued (Q&A 7): on AZERTY, this change does not alter how those keys are handled,
  so the gain is on QWERTY and in any app sending Win+Period with the `.` key.

---

## Shortcut Keys

- **Win+;** — unchanged: the key typing `;` in the layout of the window in front.
- **Win+.** — added, with the same handling: the key-down and key-up swallowed while a Windows key is
  held, the dummy key `0xE8` injected, `Pressed` posted. The `.` key is **`VK_OEM_PERIOD` whatever
  the layout** — what Chromium injects and what Windows itself answers — with **Shift up**, Ctrl and
  Alt up. On AZERTY it is the same key as Win+; (the `; .` key unshifted); Win+Shift+`; .` stays
  untouched.
- The change is local to `ShortcutHook`: `IsShortcutKey` = `IsPeriodKey || IsSemicolonKey`, the
  `.` check first — it reads no layout; `IsSemicolonKey` is unchanged. The swallowing state
  (`swallowedKey`), the dummy key and `Pressed` work for any matching key as they are. The hook's
  thread is named `Win+; and Win+. hook`.
- The exact-Shift rule stays per key: on AZERTY `;` and `.` share one key, unshifted / shifted, and
  the Shift state is what tells them apart.
- Consequence: while the app runs, **neither** Win+; nor Win+. reaches Windows' panel (its GIF,
  kaomoji and symbol tabs included). When the app ends, both go back to Windows.

---

## Documentation

- **RULES.md § Shortcut**: *Win+. is never touched* reversed; the two keys described, the menus
  injecting Win+. and the API-based ones out of reach; the agents' script check of another layout
  (activate it in the window in front, `KLF_SETFORPROCESS`, restore the user's). § Window and Tray
  Icon: the Win+; row reads *Win+; or Win+. — or an app's "Emoji — Windows+Period" menu entry*.
- **README.md / README.fr.md** (Shortcut bullet): Win+. opens the app too, and so does an app's
  "Emoji — Windows+Period" menu entry when it sends those keys (Chrome, Edge, Electron apps); an app
  opening Windows' panel by itself, without keys, still gets Windows' panel. *Win+. always does*
  (open Windows' panel) removed: only when the app is not running.
- **GLOSSARY.md / GLOSSARY.fr.md** — *Shortcut*: Win+; **and Win+.**.
- `ShortcutHook`'s comments naming Win+; only.

---

## Test Impact

No test project exists, and no earlier workfile created one: the key recognition calls the Win32
API directly (`GetForegroundWindow`, `GetKeyboardLayout`, `VkKeyScanExW`, `GetAsyncKeyState`).
Checked **by hand** and from a script — `SendInput`, as RULES.md § Shortcut asks — not by unit tests
(Q&A 8): **no test file is created or updated**.

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| Win+. (`VK_OEM_PERIOD`) shows / hides the window, on AZERTY and on QWERTY | — (script check) | — |
| Chromium's sequence (Win up before `.` up) shows the window, no Start menu opening | — (script check) | — |
| Win+; unchanged on both layouts | — (script check) | — |
| Win+Shift+. , Ctrl or Alt held → not the shortcut | — (script check) | — |

---

## Open Questions

- [x] ~~**Which key is `.`?** (a) `VK_OEM_PERIOD` whatever the layout — what Chromium injects and
  what Windows itself answers — Shift up; (b) the key typing `.` in the layout (`VkKeyScanExW('.')`,
  like `;`) — on AZERTY that is Shift + the `; .` key, which Chromium never sends; (c) both.~~ →
  (a) `VK_OEM_PERIOD`, whatever the layout, Shift up (Q&A 5).
- [x] ~~**Menus opening the panel through an API** (`CoreInputView.TryShow`): accept the limit, or
  explore detecting Windows' panel window?~~ → Accepted: out of reach, said in the README (Q&A 6).
- [x] ~~**Why does Edge's menu do nothing today**, when Chromium's keys sent from a script work on
  AZERTY?~~ → Not pursued: the user is not interested in Edge in particular (Q&A 7). The run checks
  Chromium's injected sequence from a script, on both layouts.
- [x] ~~**Unit tests**: by hand, or a first test project?~~ → By hand, as every earlier workfile
  (Q&A 8).

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-08

Initial design from the user's request and the scoping pass: Win+. caught like Win+;, in
`ShortcutHook` only. Research shows Chromium-based menus inject Win+Period with `SendInput`, which
the hook can see; API-based menus cannot be caught. On AZERTY the injected keys already match Win+;,
and a script replaying them does open the window — the Edge failure is left to the user's test.

### Iteration 2 — 2026-10-08

Every open question answered (Q&A 5–8): the `.` key is `VK_OEM_PERIOD` whatever the layout, Shift
up; API-based menus are an accepted limit, written in the README; the Edge failure of the capture is
not pursued — the user is not interested in Edge in particular; checks by hand and by script, no test
project.

### Iteration 3 — 2026-10-08 — ✅ Implemented

Go given: code, tests and documentation, in a worktree (`.claude/worktrees/win-period-shortcut`,
branch `feature/win-period-shortcut`). The design above is the frozen scope.

### Iteration 4 — 2026-10-08 — 🧭 Implementation choices

No rule broken. Choices the frozen design did not state:

- **`IsShortcutKey`** = `IsPeriodKey || IsSemicolonKey`, the `.` check first since it reads no
  layout; `IsSemicolonKey` untouched. The hook's thread renamed `Win+; and Win+. hook`.
- **RULES.md** got two lines the design did not list: the window table's Win+; row now names Win+. and
  the menu entry, and § Shortcut tells agents how to check another layout from a script.
- **Script check** (scratchpad, not committed): a test form in front, its layout activated with
  `ActivateKeyboardLayout(…, KLF_SETFORPROCESS)` **after** it takes the front — the layout is global
  on this machine and came back to `040C` as soon as the form lost the front, which first made the
  "QWERTY" cases run on AZERTY. The user's layout was restored at the end (`040C` checked).
- **Results**, every case passed on visibility:
  - QWERTY: Win+. in Chromium's order and in the natural order → shown and in front; Win+;
    (`VK_OEM_1`) → shown and in front; Win+Shift+. and Ctrl+Win+. → nothing.
  - AZERTY: the `; .` key → shown; Win+Shift+`; .` → nothing; pressed again with the window in front
    → hidden, the form back in front.
  - No Start menu opened. One AZERTY run showed the window without bringing it in front: another
    window (`Chrome_WidgetWin_1`) had just taken the foreground — probably the user's own activity during the
    check; an earlier run of the same case put it in front.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3 | 2026-10-08 | `ShortcutHook`: `IsPeriodKey`, `IsShortcutKey` |
| Unit tests | 3 | 2026-10-08 | None, as decided (Q&A 8): checked from a script, see Iteration 4 |
| README | 3 | 2026-10-08 | `README.md` and `README.fr.md`, Shortcut bullet |
| RULES.md / GLOSSARY | 3 | 2026-10-08 | RULES § Shortcut and § Window and Tray Icon; *Shortcut* row of both glossaries |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | What should Win+. do once caught? | Exactly what Win+; does | 2026-10-08 |
| 2 | With the app closed, does the menu's "Emoji — Windows+Point" open Windows' panel? | Yes, the panel opens | 2026-10-08 |
| 3 | Which menus should open the app? | Every app that has this menu | 2026-10-08 |
| 4 | Straightforward or tricky / long? | Straightforward | 2026-10-08 |
| 5 | Which key is `.`? | `VK_OEM_PERIOD` whatever the layout | 2026-10-08 |
| 6 | Menus opening the panel through an API? | Accept the limit | 2026-10-08 |
| 7 | What does Edge's menu do with the check instance running, and which layout? | "M'en fout de Edge" — not interested in Edge in particular; not tested | 2026-10-08 |
| 8 | Unit tests? | By hand | 2026-10-08 |

---

*Last updated: 2026-10-08*
