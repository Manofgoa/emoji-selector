# Global Hotkey

> Working document — Win+; opens the app's window under the text cursor of the app being typed in,
> in place of Windows' own emoji panel while the app runs; Windows' panel comes back as soon as the
> app is not running.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Today the window is shown only by a left click on the tray icon, at the place it was left. This task
adds the **global shortcut** the README announces as planned:

- **Win+;** shows the window from any app, **under the text cursor** of the app that has the focus,
  or as near as can be found;
- pressed again while the window is shown in front, it **hides** it (a toggle, like the tray icon);
- it **replaces Windows' own emoji panel** while the app runs. When the app is not running, nothing
  intercepts Win+; and Windows' panel opens as usual — no setting, no cleanup.
- **Win+.** is left alone: it keeps opening Windows' panel, app running or not.

Components touched: the new `Input/ShortcutHook.cs` (the keyboard hook), the new
`Input/CaretLocator.cs` (where the text cursor is) and its interop `Input/AccessibilityInterop.cs`,
the new `UI/WindowPlacement.cs` (where the window goes), `UI/MainForm.cs` (wiring, the toggle), the
docs. `Program.cs`, `ForegroundTracker` and
`EmojiInserter` are unchanged.

---

## Shortcut

### Why a Low-Level Keyboard Hook

Win+; is taken by Windows: `RegisterHotKey` fails on it. The app installs a **low-level keyboard
hook** (`SetWindowsHookEx(WH_KEYBOARD_LL)`), sees every key press system-wide, and **swallows** the
Win+; one so Windows never sees it. The hook lives as long as the process: when the app ends — Exit,
a crash, the Task Manager — Windows removes it and its own panel answers Win+; again.

| Point | Design |
|---|---|
| Class | `Input/ShortcutHook.cs`, created and disposed by `MainForm`. The hook is installed on a **thread of its own**, running its own message loop (the hook is called on the installing thread's loop): on the UI thread, every key typed in any app would wait whenever the UI is busy — the first rendering of the grid takes seconds — and Windows would end up removing the hook |
| Event | `Pressed`, raised on the UI thread. The hook callback does nothing but recognise the keys and **post** the event (the UI thread's `SynchronizationContext`): Windows silently removes a low-level hook whose callback is too slow (`LowLevelHooksTimeout`) |
| The `;` key | The key that types `;` in the **keyboard layout of the window in front** (`VkKeyScanEx`), with the modifiers that layout needs for it: none for `VK_OEM_1` on QWERTY nor for the `; .` key (`VK_OEM_PERIOD`) on AZERTY — where Win+Shift+(`; .`) stays Windows' (Q&A 5) |
| Win key | Left or right Windows key held down, read with `GetAsyncKeyState` when the `;` key goes down |
| Other modifiers | Ctrl, Alt or Shift held → not the shortcut, passed on to Windows (Win+Shift+; and the others keep their meaning) |
| Swallowed | The `;` key-down **and** its matching key-up. The Win key's own events are never swallowed |
| Auto-repeat | Holding Win+; raises `Pressed` once: the repeated key-downs are swallowed without raising it again, until the key-up |
| Start menu | Windows opens the Start menu when the Win key goes down then up with no other key between — which is what it sees once `;` is swallowed. The hook **injects a dummy key** (`0xE8`, an unassigned virtual-key code, key-down and key-up, `SendInput`) right after swallowing `;`, so the Win key's release opens nothing. The injected events carry a marker in `dwExtraInfo` the hook recognises and lets through untouched |
| Elevated window in front | A non-elevated app's hook is not called while an administrator window has the focus (UIPI): Win+; then opens Windows' own panel there — which is the right outcome, since the app could not type into that window anyway (`EmojiInserter` remarks) |

### What Win+; Does

The same three cases as the tray icon's left click, with a different placement:

| Window state | Win+; |
|---|---|
| Hidden | Shown **placed under the text cursor** (see *Placement*), restored if minimized, brought to the foreground |
| Shown, covered by another window | Same as hidden: placed again under the text cursor of the window now in front, brought to the foreground |
| Shown and in front (not covered) | Hidden; the **previous window** gets the foreground back, so typing resumes where it was |

- *Covered* is `MainForm.IsCovered`, the tray icon's test, unchanged.
- **Maximized** (its last state was maximized): shown maximized, not placed — placing a maximized
  window means nothing.
- **Foreground**: a process that is not in front may not take the foreground (`SetForegroundWindow`
  refuses and flashes the taskbar button). The dummy key injected by the hook makes the app the last
  one to have sent input, which lets it take the foreground (`Form.Activate`); should Windows still
  refuse, the input of the thread in front is attached to the UI thread (`AttachThreadInput`) for
  one more `SetForegroundWindow`.
- **Shown from hidden**: placed before being shown, so it does not appear at its old place first,
  then placed again once shown — its frame can be read then, and a move onto a monitor of another
  DPI may have resized it. Hidden minimized, it is shown, restored, then placed.
- The **tray icon's left click is unchanged**: the window comes back where it was left, not under the
  text cursor.

---

## Placement

### Where the Text Cursor Is

`Input/CaretLocator.cs` finds a rectangle in the **previous window**, in screen coordinates, trying in
this order and keeping the first that answers:

| # | Source | Answers for |
|---|---|---|
| 1 | `GetGUIThreadInfo` on the previous window's thread: `hwndCaret` + `rcCaret` | Classic Win32 editors (Notepad's edit control, dialog fields, many older apps) |
| 2 | MSAA: `AccessibleObjectFromWindow(focus, OBJID_CARET)` → `accLocation` | Apps that expose the caret to accessibility tools without a Win32 caret (several Chromium / Electron builds, Firefox) |
| 3 | UI Automation: the focused element's `TextPattern2.GetCaretRange` → `GetBoundingRectangles` | Modern apps: Chromium, Edge, WinUI, WPF, Office |
| 4 | UI Automation: the **focused element's** `BoundingRectangle` | The field has the focus but exposes no caret |

- Sources 3 and 4 use the element having the keyboard focus **only when it belongs to the previous
  window's process**: the focus may sit in the taskbar, clicked just before Win+;.
- Source 3: a caret range is empty; when it has no rectangle, it is expanded to the character after
  it, whose left edge and height give the caret's.
| 5 | The **mouse pointer**'s position | Nothing above answered (or no previous window) |

- An empty rectangle, or one outside every monitor, does not count: the next source is tried.
- UI Automation and MSAA are Windows' own COM components, declared by hand like `Drawing/Direct2DInterop.cs`
  — no new dependency (CONTRIBUTING § Pull requests).
- **Time limit**: sources 2–4 ask the other app's process, which may be slow or hung. They run off
  the UI thread, under one overall limit of **200 ms**; past it, the pointer (5) is used. The window
  then shows without waiting on an unresponsive app.

### Where the Window Goes

`UI/WindowPlacement.cs` computes the window's location from the found rectangle, the window's size
and the **working area** of the monitor holding the rectangle (`Screen.FromRectangle`, the taskbar
excluded). A pure function, no Windows call, so it can be tested.

| Point | Design |
|---|---|
| Below | The window's top-left corner goes under the rectangle's bottom-left corner, a small gap (4 px, scaled to the monitor's DPI) between them — like Windows' panel |
| Above | Not enough room below in the working area → the window's bottom-left corner goes above the rectangle's top-left corner, same gap |
| Neither fits | (a focused element as tall as the screen) → the window is clamped inside the working area |
| Frame | What is placed is the window's **visible frame** (DWM extended frame bounds), not its bounds with the invisible resize borders; the borders' size is read when the window is shown, and kept for the next time it is placed hidden |
| Horizontal | Left edges aligned; shifted left as far as needed to stay inside the working area |
| Pointer (5) | The pointer is treated as an empty rectangle at its position: the window goes just below-right of it, same rules |
| Size | The window keeps its current size: only its location changes |
| DPI | The app is `PerMonitorV2`: every coordinate is in physical pixels, the gap scaled to the target monitor's DPI |

---

## Documentation

| File | Change |
|---|---|
| `README.md`, `README.fr.md` | The shortcut moves from *Planned* to the features: Win+; opens the window under the text cursor, toggles it, replaces Windows' panel while the app runs; Win+. untouched |
| `RULES.md` | *Window and Tray Icon* table: a Win+; row. A *Shortcut* section: the hook, swallowing, the dummy key, the callback that only posts, the placement order |
| `GLOSSARY.md`, `GLOSSARY.fr.md` | New terms: **Shortcut** (*raccourci*) — Win+;; **Text cursor** (*curseur de texte*) — the blinking insertion point the window is placed under |
| `workfiles/TODO-FEATURES.md` | The *Show-window shortcut* row marked delivered here (a fixed Win+;, no shortcut chosen by the user — Q&A 7). Two rows added, *To design*: **Turn the Win+; capture off** — an item of the tray icon's right-click menu giving Win+; back to Windows while the app runs (Q&A 6); **Start with Windows** — launch the app at sign-in, since Win+; works only while it runs (Q&A 7) |

---

## Test Impact

**No unit test** — deliberately: there is no test project (CONTRIBUTING § Build) and the user chose
to keep checking by hand, as in the earlier workfiles, rather than create one for `WindowPlacement`
(Q&A 8).

| Behaviour to pin | Test file | Create / Update |
|---|---|---|
| — none (checked by hand) | — | — |

Manual checks:

- Win+; in Notepad, Chrome / Edge, VS Code, Word, a WinUI app: the window opens under the text
  cursor; clicking an emoji inserts it there.
- Win+; again with the window in front: hidden, typing resumes in the editor.
- No Start menu after releasing Win; Win+. still opens Windows' panel; Win+Shift+; untouched.
- Text cursor at the bottom of the screen: window above it. Near the right edge: shifted left.
- App exited: Win+; opens Windows' panel.
- An administrator window in front: Windows' panel opens.

---

## Open Questions

- [x] ~~**AZERTY**: on a French keyboard, `;` and `.` share one key (`.` with Shift). Win+; is taken to
  mean *Win + the key that types `;` in the current layout*, Shift not held — so on AZERTY the `; .`
  key, while Win+Shift+(`; .`) stays Windows'. Correct?~~ → Yes, by layout (`VkKeyScanEx`)
- [x] ~~**Turning it off**: is a way to give Win+; back to Windows **while the app runs** wanted (e.g. a
  check item in the tray icon's menu), or is exiting the app enough?~~ → Not in this task: a backlog
  row in `TODO-FEATURES.md`, an item of the tray icon's right-click menu turning the capture off
- [x] ~~**Backlog**: the *Show-window shortcut* row of `TODO-FEATURES.md` reads *chosen by the user*.
  This workfile delivers a fixed Win+;. Keep a row for choosing another shortcut? And add one for
  **starting with Windows** — the shortcut only works while the app runs?~~ → No row for choosing
  another shortcut (the row is marked delivered); a *Start with Windows* row is added
- [x] ~~**Unit tests**: keep checking by hand, as the earlier workfiles did, or create a test project
  for `WindowPlacement` (below / above / clamped / shifted left)?~~ → By hand, no test project

---

## Design Iterations

Chronological log of design refinements, of the choices the implementation run
took on its own — flagged `🧭 Implementation choices` — and of every adjustment
requested afterwards — flagged `⚙️ Post-implementation`. One entry per request,
in the order the requests were made.

### Iteration 1 — 2026-10-07

Initial design from the user's request (*a shortcut opening the window, shown under the focused
editor if possible; Win+; if possible, Windows' own panel when the app is not running*) and the
scoping answers (Q&A 1–4): Win+; only, intercepted by a low-level keyboard hook that swallows it —
so Windows' panel comes back by itself when the app is not running; a toggle like the tray icon's;
placement under the text cursor found by a cascade (Win32 caret, MSAA caret, UI Automation caret,
focused element), falling back on the mouse pointer; the window below the cursor, above it when there
is no room. Single scout pass (the subject was judged straightforward), done directly: the questions
chained through the same three files (`MainForm`, `TrayIcon`, `ForegroundTracker`).

### Iteration 2 — 2026-10-07

Open Questions answered (Q&A 5–8): the `;` key is resolved **by keyboard layout**; turning the
capture off while the app runs is **not in this task** — it goes to the backlog as a tray menu item;
the *Show-window shortcut* backlog row is marked delivered (no user-chosen shortcut), a *Start with
Windows* row is added; **no unit test**, checked by hand. Domain sections updated: *Shortcut* (the
`;` key), *Documentation* (`TODO-FEATURES.md` rows), *Test Impact*.

### Iteration 3 — 2026-10-07 — ✅ Implemented

Go given (Q&A 9): code and documentation — no unit test, as decided (Q&A 8). The run works on
`main`, a deliberate choice of the user (Q&A 10). Scope frozen: the sections above as they stand.

### Iteration 4 — 2026-10-08 — 🧭 Implementation choices

No project rule broken. Choices the frozen design did not state, or substituted variants:

1. **The hook runs on a thread of its own** — the design said the UI thread. Divergent, the closest
   workable variant: the app's launch keeps the UI thread busy for seconds (the grid's first
   rendering — Windows even showed the window as *Not responding*), and a hook on that thread makes
   every key typed in any app wait, until Windows removes it.
2. **The Win key is read with `GetAsyncKeyState`**, not tracked from the hook's events: a Win key
   pressed while an elevated window was in front never reaches the hook, so tracking could get stuck.
3. **The `;` key's modifiers are the ones the layout needs** (`VkKeyScanEx`'s Shift bit) rather than
   *never Shift*: identical on QWERTY and AZERTY, and Win+; still works on a layout typing `;` with
   Shift (German QWERTZ: Shift+`,`) instead of never matching. Ctrl and Alt never.
4. **Interop in its own file**, `Input/AccessibilityInterop.cs` (UI Automation, MSAA), like
   `Drawing/Direct2DInterop.cs`.
5. **UI Automation's focused element is used only when it belongs to the previous window's process**;
   an empty caret range is expanded to the next character for its rectangle.
6. **Foreground**: `Form.Activate`, then `AttachThreadInput` + `SetForegroundWindow` if Windows still
   refused — `Activate` alone succeeded in every check.
7. **Placement**: the visible frame is placed (the invisible resize borders taken off), before and
   again after showing; a window hidden minimized is shown, restored, then placed.
8. Dummy key `0xE8`; the injected events' marker is `0x454D4F4A`.

Checked by hand in the run (keys sent with `SendInput`, as RULES.md says): Win+; shows the window and
takes the foreground; pressed again with the window in front, it hides it and the previous window
(the Claude desktop app, Chromium) gets the foreground back; no Start menu, no Windows panel after
the Win key's release; under the chat field at the bottom of the screen, the window went **above**
it. Insertion after Win+; was seen in the test editor's text (a WinForms `TextBox`). Not checked in
the run: Win+. and Win+Shift+;, the app exited, an elevated window, Notepad / VS Code / Word / WinUI
apps, a second monitor of another DPI — left to the manual checks.

Noticed, not in scope: the first rendering of the grid at launch takes about 20 s of CPU, Windows
showing the window as *Not responding* meanwhile — the *Fast emoji display* workfile's subject.

---

## Implementation Log

Which delivery steps are done, and in which iteration. A step that does not apply
says so rather than staying blank.

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | 3, 4 | 2026-10-08 | WindowPlacement, CaretLocator + interop, ShortcutHook, MainForm wiring — 4 commits |
| Unit tests | 2 | 2026-10-07 | Not applicable — no test project, checked by hand (Q&A 8) |
| README | 3 | 2026-10-08 | README and README.fr; also glossary (EN/FR), RULES, TODO-FEATURES — 4 commits |

---

## Q&A Log

Questions asked by the agent during design, with user responses.

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Which shortcut(s) does the app intercept? | Win+; only — Win+. keeps opening Windows' panel | 2026-10-07 |
| 2 | Where does the window go when the text cursor cannot be found? | Near the mouse: under the text cursor, else under the focused field, else at the pointer | 2026-10-07 |
| 3 | What does the shortcut do when the window is already shown? | Hides it (a toggle), like Windows' panel and the tray icon's left click | 2026-10-07 |
| 4 | Is the subject straightforward or tricky / long to explore? | Straightforward — one scout pass | 2026-10-07 |
| 5 | AZERTY: Win+; is Win + the key typing `;` in the current layout, Shift not held? | Yes, by layout | 2026-10-07 |
| 6 | A way to give Win+; back to Windows while the app runs? | Not now: add to `TODO-FEATURES.md` a right-click option on the tray icon turning the Win+; capture off | 2026-10-07 |
| 7 | Backlog: keep a row for choosing another shortcut; add one for starting with Windows? | Start with Windows only | 2026-10-07 |
| 8 | Unit tests: by hand, or a test project for `WindowPlacement`? | By hand | 2026-10-07 |
| 9 | Start the implementation? Scope and where | Code, tests and documentation — current checkout | 2026-10-07 |
| 10 | On `main`: which branch? | Stay on `main` | 2026-10-07 |

---

*Last updated: 2026-10-08*
