# No Taskbar Button and Exe Icon

> Working document — the window leaves the taskbar and Alt+Tab, like Windows' emoji panel, and the exe gets
> the 🙂 icon.
> This file is the source of truth for the planned work until implemented,
> then the log of every adjustment made to it afterwards.

---

## Overview

Two small changes requested together:

1. **No taskbar button**: while the window is shown, the taskbar shows an `Emoji Selector` button (the
   window's title, second title included). The user wants it gone — and the window out of **Alt+Tab** too,
   like Windows' own Win+; panel. The tray icon stays the only way the app shows itself.
2. **Exe icon**: `EmojiSelector.exe` has the default .NET icon (no `ApplicationIcon`). It gets the
   **🙂 smiley**, the tray icon's default emoji (`TrayIcon.DefaultEmoji`) — in the File Explorer, the
   startup shortcut, the Task Manager, and as the window's own icon.

Relevant components: `UI/MainForm.cs` (window creation), `UI/WindowFrame.cs` (frame hit test),
`EmojiSelector.csproj`, `RULES.md`, `README.md` / `README.fr.md`, `CONTRIBUTING.md`, `GLOSSARY.md` /
`GLOSSARY.fr.md`.

---

## Taskbar and Alt+Tab

- `MainForm` becomes a **tool window**: `CreateParams` adds `WS_EX_TOOLWINDOW` to the extended style.
  An **unowned** tool window has no taskbar button, no Alt+Tab entry, no Task View (Win+Tab) entry.
- **Not `ShowInTaskbar = false`**: WinForms implements it by making the form **owned** by a hidden parking
  window. An owned window still appears in Alt+Tab, and .NET's `Process.MainWindowHandle` skips owned
  windows — the agents' survival check (`Get-Process EmojiSelector` → `MainWindowTitle`, see `CLAUDE.md`
  § Launch) would find no title.
- Unchanged: the window's `Text` (`Emoji Selector — <second title>`), the tray icon's tooltip, every way of
  showing / hiding it (tray click, Win+;, Win+., a later launch), the foreground handling.
- **The second title** is now seen in the **tray icon's tooltip** only — and by scripts through
  `MainWindowTitle`. The window has no title bar, no taskbar button, no Alt+Tab entry.
- **Frame to check at implementation**: the frame is Windows' own (RULES § Frame — `WM_NCCALCSIZE`, the
  side / bottom resize borders, the shadow, the rounded corners). A tool window with `WS_THICKFRAME` keeps
  the sizing frame (`SM_CXSIZEFRAME`, so `WindowFrame.ResizeBorder` holds), but Windows 11 may draw its
  corners differently: if the corners come out square, `DwmSetWindowAttribute(DWMWA_WINDOW_CORNER_PREFERENCE,
  DWMWCP_ROUND)` forces them round. The shadow, the resize borders, the drag area (`HTCAPTION`, snap,
  system menu on right click) are checked the same way.
- `GroupNameDialog` already has `ShowInTaskbar = false`; the message boxes are owned by the window: no
  taskbar button either. Nothing to change there.

---

## Exe Icon

- **Source**: Microsoft's open-source **Fluent Emoji** (`github.com/microsoft/fluentui-emoji`, **MIT**),
  `assets/Slightly smiling face/Color/slightly_smiling_face_color.svg` — the *Color* style, the design
  Segoe UI Emoji draws on Windows 11. Not a render of Segoe UI Emoji: the font's license does not clearly
  allow redistributing a render in a public repository.
- **Files**, in `src/EmojiSelector/AppIcon/`:

  | File | Holds |
  |---|---|
  | `slightly_smiling_face_color.svg` | The source, as published |
  | `LICENSE` | Fluent Emoji's MIT license, as published |
  | `app.ico` | Generated from the SVG by the script below and committed: 16, 20, 24, 32, 40, 48, 64 and 256 px, transparent background, each one a PNG entry |
  | `New-AppIcon.ps1` | The **generation script**, re-runnable: reads the SVG next to it, writes `app.ico` next to it |

- **Exe**: `<ApplicationIcon>AppIcon\app.ico</ApplicationIcon>` in `EmojiSelector.csproj` — the Win32 icon
  resource: File Explorer, the startup shortcut (`StartupShortcut` already points to the exe's icon), the
  Task Manager.
- **Window**: `MainForm.Icon` is the same icon, **fixed** — never the emoji chosen for the tray (`Use as
  tray icon` keeps changing the tray icon only). `app.ico` is also an `EmbeddedResource`
  (`EmojiSelector.AppIcon.app.ico`), read with `new Icon(stream)`: the multi-size icon, so Windows picks the
  small and the large size itself — `Icon.ExtractAssociatedIcon` gives one 32 px image.
- The **tray icon** is unchanged: rendered at run time from Segoe UI Emoji, 🙂 by default.
- **Generation** — `New-AppIcon.ps1`, committed and re-runnable: the SVG rendered at each size with a
  transparent background, then the PNGs packed into the `.ico` (an `ICONDIR` header, one `ICONDIRENTRY` per
  size, the PNGs after them). Only what Windows ships — no download, no new dependency: the renderer is
  chosen at implementation (Microsoft Edge headless, shipped with Windows 11, is the first candidate). The
  script reads the committed SVG; it never fetches it. `CONTRIBUTING.md` § App icon says how to run it and
  where the SVG comes from. The script is not part of the build: `app.ico` is committed.

---

## Documentation

| File | Change |
|---|---|
| `RULES.md` § Command-Line Arguments | The second title: shown in the tray icon's tooltip — no longer the taskbar and Alt+Tab |
| `RULES.md` § Window and Tray Icon | The window is a tool window: no taskbar button, no Alt+Tab entry, never `ShowInTaskbar = false` (why); its icon is the exe's, fixed |
| `RULES.md` § Frame | What the tool window keeps (and the corner preference, if forced) |
| `README.md` / `README.fr.md` § Features | *Second title*: tray tooltip only. *Window*: no taskbar button, not in Alt+Tab, like Windows' emoji panel. The exe's 🙂 icon |
| `README.md` / `README.fr.md` § Tech | The icon is Fluent Emoji's (MIT), link to `CONTRIBUTING.md` § App icon |
| `CONTRIBUTING.md` § App icon | New: source, license, sizes, how to run `New-AppIcon.ps1` |
| `GLOSSARY.md` / `GLOSSARY.fr.md` | New term **App icon** (*icône de l'application*): the 🙂 icon of the exe — File Explorer, the startup shortcut, the Task Manager — and of the window, Fluent Emoji's design, fixed; not the **tray icon**, which can show another emoji |

`CLAUDE.md` § Launch is unchanged: the survival check by `MainWindowTitle` keeps working on an unowned
window.

---

## Test Impact

The repository has **no test project** (`src/EmojiSelector.slnx` holds the app only), and nothing here is
unit-testable logic: a window style and a resource. **No unit test created or updated.**

The checks are manual / scripted at implementation:

| Behaviour to pin | How |
|---|---|
| No taskbar button, no Alt+Tab entry | Extended style read with `GetWindowLong(GWL_EXSTYLE)`; screenshot of the taskbar with the window shown |
| `Get-Process EmojiSelector` still gives the `MainWindowTitle` | The survival check itself, with `--title` |
| Frame unchanged (rounded corners, shadow, resize borders, drag area) | Screenshot; scripted resize (RULES § Size) |
| The exe's icon | `[System.Drawing.Icon]::ExtractIcon` on the built exe, every size present |

---

## Open Questions

- [x] ~~Taskbar: no button, button without text, Alt+Tab?~~ → No button **and** out of Alt+Tab (tool window)
- [x] ~~The window's icon: the exe's, or the tray's chosen emoji?~~ → The exe's, fixed
- [x] ~~Where the 🙂 image comes from?~~ → Fluent Emoji *Color* SVG (MIT), `.ico` generated once and committed
- [x] ~~Commit the script generating `app.ico` from the SVG (re-runnable), or only describe the procedure in
  `CONTRIBUTING.md` § App icon?~~ → Script committed: `AppIcon/New-AppIcon.ps1`
- [x] ~~Add a glossary term **App icon** (*icône de l'application*) — the exe's 🙂, distinct from the **tray
  icon** that can show another emoji?~~ → Yes, in both glossaries

---

## Design Iterations

### Iteration 1 — 2026-10-08

Initial design from the request and the scoping answers (Q&A 1–4): the window becomes an unowned tool
window (`WS_EX_TOOLWINDOW`, never `ShowInTaskbar = false`), out of the taskbar and Alt+Tab; the exe and the
window get a fixed 🙂 icon generated from Fluent Emoji's MIT SVG, committed as a multi-size `app.ico`. Two
questions left: a committed generation script, a glossary term.

### Iteration 2 — 2026-10-08

Q&A 5–6: the `app.ico` generation script is committed (`AppIcon/New-AppIcon.ps1`, re-runnable, Windows'
own tools only, reading the committed SVG), and the glossary gets the term **App icon**, in English and in
French. No open question left.

---

## Implementation Log

| Step | Iteration | Date | Notes |
|---|---|---|---|
| Code | | | |
| Unit tests | | | No test project — nothing to create (see *Test Impact*) |
| README | | | |
| RULES | | | |
| CONTRIBUTING | | | |
| GLOSSARY | | | |

---

## Q&A Log

| # | Question | Answer | Date |
|---|---|---|---|
| 1 | Taskbar while the window is open: no button (Alt+Tab kept), no button and no Alt+Tab, or button without text? | No button, no Alt+Tab | 2026-10-08 |
| 2 | The window's icon: the exe's, fixed, or following the tray's chosen emoji? | The exe's, fixed | 2026-10-08 |
| 3 | Source of the 🙂 image for the `.ico`: Fluent Emoji Color (MIT), a render of Segoe UI Emoji, or generated at build? | Fluent Emoji Color (MIT) | 2026-10-08 |
| 4 | Depth: straightforward or tricky / long? | Straightforward | 2026-10-08 |
| 5 | Commit the `app.ico` generation script, or describe the procedure only? | Script committed | 2026-10-08 |
| 6 | Add the glossary term *App icon*? | Yes | 2026-10-08 |

---

*Last updated: 2026-10-08*
