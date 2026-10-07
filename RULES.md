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
- The second title is the window's `Text`, shown in the **taskbar** and **Alt+Tab** — the window
  has no title bar — and in the **tray icon's tooltip**. It is **not persisted**.
- It exists for the agents: every launch by Claude Code passes the session's name in it (see
  `CLAUDE.md` § Launch), so instances running side by side tell which implementation they test.

## Window and Tray Icon

The app is resident: it lives in the notification area as long as it runs (`UI/TrayIcon.cs`).

| Action | Does |
|---|---|
| Close cross ✕ right of the tabs, Alt+F4 (`CloseReason.UserClosing`) | Hides the window to the tray — the app keeps running |
| Drag area — the empty band between the last tab and the settings button | Moves the window; right click → Windows' system menu |
| Settings button ⚙ left of the close cross → `Open app folder` | Opens the exe's folder in the File Explorer, the exe selected; the window stays |
| Tray icon, left click | Hidden → shown; covered by another window → brought to the front; already in front → hidden |
| Tray icon, right click → `Exit` | Ends the app |
| Win+; | Hidden or covered → shown **under the text cursor** of the previous window and brought to the front; already in front → hidden, the previous window getting the foreground back (see *Shortcut* below) |
| Emoji clicked in the grid | Inserted into the **previous window**, then the window hides to the tray (see *Insertion* below) |
| Enter in the search box | Inserts the first result, like a click on it (see *Search Box* below) |
| Esc in the search box | Clears the box; already empty → hides the window to the tray |
| Any other close reason — Windows shutting down, the Task Manager, a `WM_CLOSE` sent by another process | Ends the app, never blocked |

- The tray icon shows the **last emoji used**, every launch starting on 😊 — **never persisted**.
  `MainForm.OnEmojiUsed` is the one place telling it an emoji was used.
- Emojis are drawn **in colour** by `Drawing/EmojiRenderer.cs` (Direct2D + DirectWrite): GDI and
  GDI+ draw Segoe UI Emoji in monochrome.
- The tooltip is the window's title, second title included. A hidden window has no taskbar button.
- An agent checking the close button from a script sends `WM_SYSCOMMAND` / `SC_CLOSE`: a plain
  `WM_CLOSE` counts as the Task Manager and ends the app.

### Frame

- **No title bar**, like the Win+; panel. `MainForm` answers `WM_NCCALCSIZE` with the default client
  area, its top put back to the window's top: the caption becomes client area, while the left, right
  and bottom resize borders, the shadow and the rounded corners stay Windows' own. Never
  `FormBorderStyle.None`, which loses all three.
- The **top resize border** went with the caption: `WM_NCHITTEST` answers `HTTOP` (`HTTOPLEFT` /
  `HTTOPRIGHT` at the corners) on the top band of the client area, as thick as a side border
  (`UI/WindowFrame.cs`). The **drag area** answers `HTCAPTION`: Windows moves the window, snaps it
  to the sides of the screen, opens its system menu.
- A child control under those points — the search bar on top, the tab strip's drag area — answers
  `HTTRANSPARENT`, so the hit test reaches the window: Windows asks the control under the mouse first.
- **Never minimized nor maximized**: `MinimizeBox` and `MaximizeBox` are off, so Windows refuses
  Win+Up, Win+Down, the drag-to-top snap and the double-click on the drag area.
- The close cross and the settings button belong to the tab strip (`UI/CategoryTabStrip.cs`) and
  are never greyed by a search. The cross turns Windows red on hover; the settings button's menu is a
  `ContextMenuStrip` shown under it, owned by `MainForm`.

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
  rectangle, above it when there is no room, inside the monitor's working area. The tray icon's
  click never moves the window.
- UI Automation and MSAA are declared by hand (`Input/AccessibilityInterop.cs`), like Direct2D: no
  new dependency.
- An agent checking Win+; from a script sends it with `SendInput` — injected keys go through the
  hook: the Windows key, then the layout's `;` key.

## Categories and Insertion

- **Categories** come from `Data/EmojiCatalog.cs` only: seven tabs in the Win+; order, the Emojibase
  data embedded in the exe ([CONTRIBUTING.md § Emoji data](CONTRIBUTING.md#emoji-data)). Left out:
  components, flags, skin-tone variants. An emoji newer than the system font is kept (a box).
- The grid and the tab strip are **custom-drawn** (`UI/EmojiGrid.cs`, `UI/CategoryTabStrip.cs`);
  where things sit is computed by `UI/EmojiGridLayout.cs` alone. The tab glyphs are monochrome
  (Segoe Fluent Icons, Segoe MDL2 Assets on Windows 10) and drawn with GDI.
- **The grid never renders an emoji on the UI thread.** `Drawing/EmojiBitmapCache.cs` pre-renders
  every emoji at launch, on a background thread with its own `EmojiRenderer`, in grid order; a cell
  whose emoji is not ready yet is filled with **fluorescent green**, on purpose. A DPI change starts
  a new pre-render at the new size.
- The pre-rendered emojis are saved in **`cache\` next to the exe** — one `emojis-{size}.png` atlas
  and its `emojis-{size}.key` per emoji size — and reloaded at the next launch while the key still
  matches (size, Segoe UI Emoji's file, the emoji list, the renderer's parameters). A change to the
  rendering that the key does not see **bumps `EmojiBitmapCache.FormatVersion`**. A folder that
  cannot be written is not an error: the emojis are rendered again at every launch.
- One `EmojiRenderer` per thread, each with its own **isolated** DirectWrite factory: the shared
  one cannot cross threads.
- **Insertion** (`Input/`): the previous window is tracked by `ForegroundTracker` — the taskbar and
  the notification area are skipped, since a click on the tray icon goes through them. It is brought
  back to the foreground **before** the window hides (only the foreground app may hand it over),
  then the emoji is typed with `SendInput` / `KEYEVENTF_UNICODE` — never through the clipboard.

## Search Box

The **search box** sits above the tab strip (`UI/MainForm.cs`); the matching and the ranking live in
`Data/EmojiSearch.cs` alone.

- **Keywords**: an emoji's name and tags, in **English and French at once**, no setting.
  `compact.en.json` is the list; `compact.fr.json`, joined by hexcode, only adds keywords. Both are
  embedded with `WithCulture="false"`: without it the `.en` / `.fr` in their names make MSBuild move
  them to satellite assemblies, out of the exe.
- **Matching**: case and diacritics ignored (`é` → `e`, `œ` → `oe`); an emoji is a result when every
  typed word is **contained** in a word of its keywords.
- **Ranking**, in tiers: the whole word → its start → anywhere else; then the share of the word
  covered; then a name before a tag. Several typed words: the worst tier, then the lowest coverage,
  then the most name matches. Ties keep the catalog order. `caca` → 💩 before 🥜 *cacahuète*.
- **Every show** clears the box and focuses it (`OnVisibleChanged`). The placeholder is the native
  cue banner shown while focused (`EM_SETCUEBANNER`): `PlaceholderText` hides on focus, and the box
  always has it.
- **Search mode** (non-blank text): the tab strip is `Greyed` (clicks ignored), the grid shows one
  `Search results` section (`No emoji found` when empty) and raises no `ActiveCategoryChanged`.
  Emptying the box brings the categories back at their previous scroll position.
- The ✕ next to the box is exactly as high as it: showing it must not move the tabs.
- An agent checking the search from a script sends `WM_SETTEXT` / `WM_KEYDOWN` to the box itself,
  never global keystrokes (`SendKeys`, `SendInput`): the window may not be in front, and the keys
  would land in another app.

## Repository Docs

The repository's docs — the French versions (`README.fr.md`, `GLOSSARY.fr.md`) kept in step with
the English ones, the language line, one place per fact — follow the rules shared by every
mini-app: `../CLAUDE.md` § Repository Docs.
