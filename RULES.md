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
| Win+; | Hidden or covered → shown **under the text cursor** of the previous window and brought to the front; already in front → hidden, the previous window getting the foreground back (see *Shortcut* below) |
| Emoji clicked in the grid | Inserted into the **previous window**, then the window hides to the tray (see *Insertion* below) |
| Any other close reason — Windows shutting down, the Task Manager, a `WM_CLOSE` sent by another process | Ends the app, never blocked |

- The tray icon shows the **last emoji used**, every launch starting on 😊 — **never persisted**.
  `MainForm.OnEmojiUsed` is the one place telling it an emoji was used.
- Emojis are drawn **in colour** by `Drawing/EmojiRenderer.cs` (Direct2D + DirectWrite): GDI and
  GDI+ draw Segoe UI Emoji in monochrome.
- The tooltip is the window's title, second title included. A hidden window has no taskbar button.
- An agent checking the close button from a script sends `WM_SYSCOMMAND` / `SC_CLOSE`: a plain
  `WM_CLOSE` counts as the Task Manager and ends the app.

## Shortcut

- **Win+;** is caught by a **low-level keyboard hook** (`Input/ShortcutHook.cs`): Windows owns it,
  `RegisterHotKey` cannot have it. The hook swallows the `;` key-down and key-up while a Windows key
  is held, so Windows never sees the shortcut — and its own panel answers Win+; again as soon as the
  app ends. Win+. is never touched.
- The `;` key is the one typing `;` in the keyboard layout of the window in front (`VkKeyScanEx`),
  with the modifiers that layout needs for it: `VK_OEM_1` on QWERTY, the `; .` key on AZERTY. Ctrl or
  Alt held → not the shortcut.
- The hook runs on **its own thread**, with its own message loop, and does nothing but recognise the
  keys and post `Pressed` to the UI thread: every key typed in any app waits on the hook, and Windows
  silently removes one too slow to answer (`LowLevelHooksTimeout`). Never on the UI thread.
- Swallowing `;` leaves the Windows key going down then up with nothing between — Windows opens the
  Start menu. The hook injects a **dummy key** right away (`0xE8`, unassigned), marked in
  `dwExtraInfo` so it lets it through; being the last app to send input also lets the app take the
  foreground.
- **Placement**: `Input/CaretLocator.cs` looks for the text cursor of the previous window — the
  Win32 caret, the MSAA caret, the UI Automation caret range, the UI Automation focused element (of
  that window's app only) — else takes the mouse pointer. The sources asking the other app run off
  the UI thread, **200 ms** in all. `UI/WindowPlacement.cs` puts the visible frame under the found
  rectangle, above it when there is no room, inside the monitor's working area. A window last
  maximized comes back maximized, not placed. The tray icon's click never moves the window.
- UI Automation and MSAA are declared by hand (`Input/AccessibilityInterop.cs`), like Direct2D: no
  new dependency.
- An agent checking Win+; from a script sends it with `SendInput` — injected keys go through the
  hook: the Windows key, then the layout's `;` key.

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
