# Agent Rules — Emoji Selector

Rules every change to this app follows. Vocabulary: see [GLOSSARY.md](GLOSSARY.md).

## Command-Line Arguments

What the exe accepts, parsed in `Program.Main`:

| Argument | Does |
|---|---|
| `--title <text>` (`MainForm.TitleArgument`) | The **second title**: the window reads `Emoji Selector — <text>` |
| Anything else | Ignored |

- `--title` takes the **next argument** as its value, unless that one is an option itself; a value
  missing or blank is ignored (the plain title), it is trimmed, and given twice the last one wins.
- The second title is shown in the **window title bar** — so in the taskbar and Alt+Tab — and in
  the **tray icon's tooltip**. It is **not persisted**.
- It exists for the agents: every launch by Claude Code passes the session's name in it (see
  `CLAUDE.md` § Launch), so instances running side by side tell which implementation they test.

## Window and Tray Icon

The app is resident: it lives in the notification area as long as it runs (`UI/TrayIcon.cs`).

| Action | Does |
|---|---|
| Close button ✕, Alt+F4 (`CloseReason.UserClosing`) | Hides the window to the tray — the app keeps running |
| Minimize button _ | Hides the window to the tray; it comes back in its last non-minimized state |
| Tray icon, left click | Hidden → shown; covered by another window → brought to the front; already in front → hidden |
| Tray icon, right click → `Exit` | Ends the app |
| Emoji clicked in the grid | Inserted into the **previous window**, then the window hides to the tray (see *Insertion* below) |
| Any other close reason — Windows shutting down, the Task Manager, a `WM_CLOSE` sent by another process | Ends the app, never blocked |

- The tray icon shows the **last emoji used**, every launch starting on 😊 — **never persisted**.
  `MainForm.OnEmojiUsed` is the one place telling it an emoji was used.
- Emojis are drawn **in colour** by `Drawing/EmojiRenderer.cs` (Direct2D + DirectWrite): GDI and
  GDI+ draw Segoe UI Emoji in monochrome.
- The tooltip is the window's title, second title included. A hidden window has no taskbar button.
- An agent checking the close button from a script sends `WM_SYSCOMMAND` / `SC_CLOSE`: a plain
  `WM_CLOSE` counts as the Task Manager and ends the app.

## Categories and Insertion

- **Categories** come from `Data/EmojiCatalog.cs` only: seven tabs in the Win+; order, the Emojibase
  data embedded in the exe ([CONTRIBUTING.md § Emoji data](CONTRIBUTING.md#emoji-data)). Left out:
  components, flags, skin-tone variants. An emoji newer than the system font is kept (a box).
- The grid and the tab strip are **custom-drawn** (`UI/EmojiGrid.cs`, `UI/CategoryTabStrip.cs`);
  where things sit is computed by `UI/EmojiGridLayout.cs` alone. Emojis go through
  `EmojiRenderer`, once each, cached; the tab glyphs are monochrome (Segoe Fluent Icons, Segoe MDL2
  Assets on Windows 10) and drawn with GDI.
- **Insertion** (`Input/`): the previous window is tracked by `ForegroundTracker` — the taskbar and
  the notification area are skipped, since a click on the tray icon goes through them. It is brought
  back to the foreground **before** the window hides (only the foreground app may hand it over),
  then the emoji is typed with `SendInput` / `KEYEVENTF_UNICODE` — never through the clipboard.

## Repository Docs

The repository's docs — the French versions (`README.fr.md`, `GLOSSARY.fr.md`) kept in step with
the English ones, the language line, one place per fact — follow the rules shared by every
mini-app: `../CLAUDE.md` § Repository Docs.
