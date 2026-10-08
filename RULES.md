# Agent Rules — Emoji Selector

Rules every change to this app follows. Vocabulary: see [GLOSSARY.md](GLOSSARY.md).

## Command-Line Arguments

What the exe accepts, parsed in `Program.Main`:

| Argument | Does |
|---|---|
| `--title <text>` (`MainForm.TitleArgument`) | The **second title**: the window reads `Emoji Selector — <text>` |
| `--background` (`MainForm.BackgroundArgument`) | Starts **hidden** in the notification area, the window shown by Win+; or the tray icon (see *Start with Windows*) |
| Anything else | Ignored |

- `--title` takes the **next argument** as its value, unless that one is an option itself; a value
  missing or blank is ignored (the plain title), it is trimmed, and given twice the last one wins.
- The second title is the window's `Text`, shown in the **tray icon's tooltip** — the window has no
  title bar, no taskbar button, no Alt+Tab entry (see *Frame*) — and read by scripts as the process's
  `MainWindowTitle`. It is **not persisted**.
- It exists for the agents: every launch by Claude Code passes the session's name in it (see
  `CLAUDE.md` § Launch), so instances running side by side tell which implementation they test.

## Window and Tray Icon

The app is resident: it lives in the notification area as long as it runs (`UI/TrayIcon.cs`).

| Action | Does |
|---|---|
| Close cross ✕ right of the tabs, Alt+F4 (`CloseReason.UserClosing`) | Hides the window to the tray — the app keeps running |
| Drag area — the empty band between the last tab and the settings button | Moves the window; right click → Windows' system menu |
| Settings button ⚙ left of the close cross → `Show frequently used` | Checked while the frequent tab is shown: a click hides it or shows it again (see *Frequent Tab* below) |
| Settings button ⚙ → `Clear frequently used` | After a confirmation, every use counter reset (see *Frequent Tab* below) |
| Settings button ⚙ → `New group…` | Asks for a name, then creates a **custom group**, last, and scrolls to it (see *Custom Tab* below) |
| Settings button ⚙ → `Show groups ▸` | Every custom group, checked while shown: a click hides it or shows it again (see *Custom Tab* below) |
| Settings button ⚙ → `Show French names` | Checked while the details panel shows its French row: a click hides it or shows it again (see *Details Panel* below) |
| Settings button ⚙ → `Highlight color…` | Windows' colour dialog: the colour highlighting the search's matches in the details panel (see *Details Panel* below) |
| Settings button ⚙ → `Reset window size` | Back to the **default size** right away, the top-left corner kept — moved only as far as needed to stay inside the working area —, and the saved size removed (see *Size* and *Corner* below); the window stays |
| Settings button ⚙ → `Window opacity ▸` | `100%`, `98%`, `96%`, `94%`, `92%`, the one in use checked: a click applies it to the whole window at once and saves it (see *Opacity* below); the window stays |
| Settings button ⚙ → `Open app folder` | Opens the exe's folder in the File Explorer, the exe selected; the window stays |
| Settings button ⚙ → `Start with Windows` | Checked while Windows starts this exe at sign-in: a click writes or deletes the **startup shortcut** (see *Start with Windows* below); the window stays |
| Settings button ⚙ → `Check for emoji updates…` | The latest Emojibase version online; a newer one offered, downloaded, then a restart offered (see *Emoji Data* below) |
| Settings button ⚙ → `Reset all settings…` | After a confirmation, every file the app wrote deleted, then a restart (see *Reset All Settings* below) |
| Tray icon, left click | Hidden → shown in the **corner** of the mouse's monitor (see *Corner* below); covered by another window → brought to the front, not moved; already in front → hidden |
| Tray icon, right click → `Exit` | Ends the app |
| Win+; or Win+. — or an app's "Emoji — Windows+Period" menu entry that sends it | Hidden or covered → shown **under the text cursor** of the previous window and brought to the front; already in front → hidden, the previous window getting the foreground back (see *Shortcut* below) |
| Emoji clicked in the grid — not one of the group in reorder mode | Inserted into the **previous window**, then the window hides to the tray (see *Insertion* below); its use counted (see *Frequent Tab* below) |
| Emoji Ctrl+clicked in the grid — Ctrl alone, not one of the group in reorder mode | Inserted into the previous window, the window **kept open** (see *Keep-Open Insertion* below) |
| Emoji right-clicked in the grid — or the Menu key / Shift+F10 on the grid's selection | Its menu: `Use as tray icon`, then `Add to ▸` the custom groups, `Remove` in a group (see *Custom Tab* below), `Remove from frequently used` in the frequent section (see *Frequent Tab* below) |
| Enter, in the search box or the grid | Inserts the **selection**, like a click on it (see *Keyboard* below) — never an emoji of the group in reorder mode |
| Ctrl+Enter, in the search box or the grid | Inserts the selection like Enter, the window **kept open** (see *Keep-Open Insertion* below) |
| Esc, in the search box or the grid | Ends the reorder mode; otherwise clears the box; already empty → hides the window to the tray |
| Any other close reason — Windows shutting down, the Task Manager, a `WM_CLOSE` sent by another process | Ends the app, never blocked |

- The tray icon shows the **emoji the user chose**, 🙂 by default (`TrayIcon.DefaultEmoji`): using an
  emoji never changes it. **`Use as tray icon`**, first in an emoji's right-click menu in any section,
  a separator after it, **checked** on the emoji the icon shows: a click shows it at once and saves
  it as `trayEmoji` in `settings.json` (see *Size* below) — the default 🙂 included, there is no
  reset; the window stays. Reloaded at launch; an emoji the catalog does not have shows 🙂, its key
  left as it is until the next choice.
- The **app icon** — the exe's and the window's — is a 🙂 too, but **fixed**: Fluent Emoji's design
  (`AppIcon/app.ico`, [CONTRIBUTING.md § App icon](CONTRIBUTING.md#app-icon)), the exe's
  `ApplicationIcon` and `MainForm.Icon`, read from the embedded `.ico` with every size (Windows picks
  the small and the large one). `Use as tray icon` never changes it.
- Emojis are drawn **in colour** by `Drawing/EmojiRenderer.cs` (Direct2D + DirectWrite): GDI and
  GDI+ draw Segoe UI Emoji in monochrome.
- The tooltip is the window's title, second title included.
- An agent checking the close button from a script sends `WM_SYSCOMMAND` / `SC_CLOSE`: a plain
  `WM_CLOSE` counts as the Task Manager and ends the app.

### Frame

- **No title bar**, like the Win+; panel. `MainForm` answers `WM_NCCALCSIZE` with the default client
  area, its top put back to the window's top: the caption becomes client area, while the left, right
  and bottom resize borders, the shadow and the rounded corners stay Windows' own. Never
  `FormBorderStyle.None`, which loses all three.
- **A tool window**, like the Win+; panel: `MainForm.CreateParams` adds `WS_EX_TOOLWINDOW` and takes
  out `WS_EX_APPWINDOW` — no taskbar button, no Alt+Tab nor Task View entry. Never `ShowInTaskbar =
  false`: WinForms makes the form owned by a hidden window for it — an owned window stays in Alt+Tab,
  and `Process.MainWindowHandle` skips it, which the agents' survival check by `MainWindowTitle` needs.
- Windows 11 gives a tool window a menu's **small corner radius**: `OnHandleCreated` asks
  `DWMWCP_ROUND` (`DWMWA_WINDOW_CORNER_PREFERENCE`), the corners of an app window. Windows 10 refuses
  it, and has no rounded corners anyway.
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
- The settings menu has **five sections**, one per feature, in the window's order, separated by
  lines and **untitled** (`MainForm.CreateSettingsMenu`): *frequently used* (`Show frequently used`,
  `Clear frequently used`), *custom groups* (`New group…`, `Show groups ▸`), *details panel* (`Show
  French names`, `Highlight color…`), *window* (`Reset window size`, `Window opacity ▸`), *app* (`Open app folder`,
  `Start with Windows`, `Check for emoji updates…`, `Reset all settings…`). A new item joins the section of its feature; the labels say their
  subject on their own.
- A **colour item** shows the colour in use as its image (`MainForm.SetSwatch`): a rounded square of
  16 logical pixels, outlined in `SystemColors.ControlDark`, drawn again at each opening of the menu
  — so it follows the colour and the monitor's DPI.

### Size

- **Default size**, measured in emojis: **16 columns** wide, and high enough for a section's header
  then **8 full rows** when that section is scrolled to the top (`MainForm.DefaultColumns` /
  `DefaultRows`). Computed in `MainForm.OnLoad`, before the window is placed, from the grid's metrics
  at the window's DPI (`EmojiGrid.SizeFor`) and the heights of the search bar, the tab strip and the
  details panel (`EmojiDetailsPanel.HeightFor`) — never a hard-coded pixel size.
- The window is sized through **`MainForm.SetClientArea`**, never the `ClientSize` setter: that one
  counts a caption, which is client area here (see *Frame*) — the window would come out a caption too
  tall. The borders are read from the window itself (`GetWindowRect` / `GetClientRect`).
- **Remembered size**: when the user **finishes a resize** (`OnResizeEnd`), the client size is saved
  in **`settings.json` next to the exe**, in logical pixels (96 DPI), and reloaded at the next launch
  in place of the default. Not at exit: Windows shutting down or the Task Manager may end the app
  without running its code. A move, or a drag to a monitor of another scale, saves nothing.
- `settings.json` is the app's **shared settings file** (`Data/SettingsFile.cs`): `{ "windowWidth":
  …, "windowHeight": …, "trayEmoji": …, "showFrequent": …, "showFrench": …, "highlightColor": …, "opacity": … }`;
  a write keeps the keys it does not
  know. Written through `settings.json.new` then a replace, the emojis as themselves
  (`EmojiUsage.ReadableEmojis`).
- **The size only**: the position is never saved — placed in the corner at launch (see *Corner*
  below), Win+; places it anyway.
- Missing, unreadable or invalid file, a folder that cannot be written → the default size, never an
  error. A size larger than the working area of the monitor is reduced to fit it; `MinimumSize` wins
  over a smaller one.
- An agent checking a resize from a script sends `WM_ENTERSIZEMOVE`, a `SetWindowPos`, then
  `WM_EXITSIZEMOVE`: `OnResizeEnd` runs as after a drag, the mouse untouched.

### Corner

- **When**: the launch, and the tray icon's click — or a later launch (see *Start with Windows*) — on
  a **hidden** window, put its visible frame in the **corner** of the working area of the monitor
  holding the **mouse pointer**, next to its notification area, `WindowPlacement.CornerMargin` (12
  logical pixels) from both edges, like Windows' own flyouts (`MainForm.PlaceInCorner`,
  `WindowPlacement.PlaceInCorner`).
- **Which corner** — the notification area ends the taskbar, so the corner follows its edge, read
  from the monitor itself: the side where the working area is shorter than the bounds, the widest gap
  when several are (`WindowPlacement.TaskbarEdge`).

  | Taskbar | Corner |
  |---|---|
  | Bottom, right, or none seen (auto-hidden, not on that monitor) | Bottom right |
  | Top | Top right |
  | Left | Bottom left |

- A frame larger than the working area keeps its top-left corner in it.
- **Not moved**: a covered window the tray icon brings to the front; a window the user moved, until
  its next hidden → shown. Win+; places it under the text cursor (see *Shortcut*).
- **Placed twice**, like Win+;: before the show, so it does not appear at its old place, and after,
  once its frame can be read — `OnShown` for the launch. Windows applies the DPI of a monitor of
  another scale **after** that second placement: `OnDpiChanged` places it again while the window is
  where `PlaceInCorner` put it (`inCorner`, cleared by a user move or resize, Win+; and `Reset window
  size`).
- **`Reset window size`** keeps the top-left corner, then moves the frame up / left only as far as
  needed to stay inside the working area of its monitor (`WindowPlacement.KeepInside`): grown from the
  corner, the default size would leave it.
- An agent checking a tray click from a script cannot reach the icon (hidden in the overflow, no UI
  Automation): it **posts** the icon's callback message, `WM_USER + 1024` with `WM_LBUTTONDOWN` then
  `WM_LBUTTONUP` as `lParam`, to the app's hidden `NotifyIcon` window. Such a click gives no
  foreground right: the window shows without being in front. The script runs **Per-Monitor V2 aware**
  (`SetThreadDpiAwarenessContext(-4)`): PowerShell is system aware, and sees the coordinates of the
  monitors of another scale converted.

### Opacity

- **`Window opacity ▸`**, in the settings menu's *window* section after `Reset window size`: `100%`,
  `98%`, `96%`, `94%`, `92%` (`MainForm.OpacityPercents`), the one in use checked at each opening of the
  menu. A click applies it at once (`MainForm.SetOpacity`) and saves it; no confirmation.
- **`Form.Opacity`**: below 100 %, a layered window (`WS_EX_LAYERED`, `LWA_ALPHA`) — the whole window
  fades, search bar, tabs, grid and details panel, **always**: in front or not. The frame stays
  Windows' own (see *Frame*). The menus and dialogs are windows of their own: they stay opaque.
- Set in the constructor, before the handle exists: a `--background` launch shows the window at its
  opacity from the first show.
- **Saved** as `opacity` in `settings.json`, the percentage (`SettingsFile.ReadOpacity` /
  `WriteOpacity`). Missing, or not an integer → 100 %. An integer that is not one of the five
  (written by hand) → the **nearest**, a tie to the more opaque (`MainForm.NearestOpacity`): `97` →
  98 %, `90` → 92 %, `120` → 100 % — the key left as it is until the next choice.
- `Reset window size` leaves it alone; `Reset all settings…` brings it back to 100 %.
- An agent checking the opacity from a script cannot click the settings button: it writes `opacity`
  in the build's `settings.json`, launches the exe and reads `GetLayeredWindowAttributes` (no
  `WS_EX_LAYERED` → 100 %).

## Start with Windows

**`Start with Windows`**, in the settings menu's *app* section after `Open app folder`, has Windows
launch the app at sign-in, hidden in the notification area. Off by default.

- **The startup shortcut** (`Data/StartupShortcut.cs`): `Emoji Selector.lnk` in the user's Startup
  folder (`Environment.SpecialFolder.Startup`) — the running exe, `--background`, the exe's folder as
  working folder, the exe's icon. Written and read through `IShellLinkW` / `IPersistFile`, declared by
  hand (`Data/ShellLinkInterop.cs`): no new dependency.
- **The shortcut is the setting**: nothing in `settings.json`. The item's check mark is read each time
  the menu opens; it is **checked** only while the shortcut exists, its target is **this exe** (the
  full path, case ignored) and the Task Manager has not disabled it — *Startup apps* → *Disable* keeps
  the shortcut and writes a value `Emoji Selector.lnk` under
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder`, a
  binary whose first byte is **odd** while disabled. Missing or unreadable → enabled.
- Unchecked → a click writes the shortcut to this exe — replacing one to another exe — and deletes the
  Task Manager's value; checked → a click deletes both. No confirmation. A failure → `Could not change
  the startup shortcut`, a warning, nothing else changed.
- **Never touched unasked**: no check, no repair, no rewrite at launch.
- **`--background`**: `MainForm.SetVisibleCore` skips `Application.Run`'s show — the tray icon, the hook,
  the pre-render and the data run as on a normal launch, the window's handle is not even created. The
  first show — Win+;, Win+. or the tray icon — runs `OnLoad` then: sized, centred, and placed again
  under the text cursor by Win+; (`MainForm.showAnchor`).

### Single Instance

- **One instance per exe** (`SingleInstance.cs`): a mutex `Local\EmojiSelector-<key>`, the key a hash of
  the exe's full path, upper-cased — taken in `Program.Main` before anything is loaded. Two builds in
  two folders (a worktree's next to `main`'s) still run side by side; the same exe twice does not.
- **A later launch** of the same exe exits at once: without `--background` it first sets the event
  `Local\EmojiSelector-<key>-show` — the first instance's window is shown and brought to the front, as
  the tray icon's click does it (`MainForm.ShowOrBringToFront`: a hidden one in the corner, see
  *Corner*), never hidden — after `AllowSetForegroundWindow(ASFW_ANY)`; with
  `--background` (a sign-in while the app runs) it exits silently. Its arguments, `--title` included,
  are ignored.
- The first instance waits for the event on a thread of its own, the show posted to the UI thread.
- The **restart** after an emoji update releases the mutex and the event before starting the exe
  again (`MainForm.Restart`): `Application.Restart` would be found by the old instance, and would pass
  `--background` along.
- A mutex or an event that cannot be created → the app runs without the single instance, never an
  error.
- An agent's check instance of an exe takes the single instance: the delivery launch of the same exe
  would only show that instance's window. The check instance is ended first.
- An agent checking the startup shortcut cannot click the settings button from a script: the
  shortcut is checked by calling `StartupShortcut` by reflection on the built dll — the shortcut then
  targets the host (`pwsh.exe`), and is deleted at the end.

## Shortcut

- **Win+;** and **Win+.**, the same shortcut, are caught by a **low-level keyboard hook**
  (`Input/ShortcutHook.cs`): Windows owns both, `RegisterHotKey` cannot have them. The hook swallows
  the `;` or `.` key-down and key-up while a Windows key is held, so Windows never sees the shortcut —
  and its own panel answers both again as soon as the app ends.
- The `;` key is the one typing `;` in the keyboard layout of the window in front (`VkKeyScanEx`),
  with the modifiers that layout needs for it: `VK_OEM_1` on QWERTY, the `; .` key on AZERTY.
- The `.` key is **`VK_OEM_PERIOD` whatever the layout**, Shift up — the key Windows answers Win+. on.
  On AZERTY it is the `; .` key unshifted, Win+; itself; Win+Shift+`; .` is not the shortcut.
- Ctrl or Alt held → not the shortcut, for either key.
- **Menus**: an app's "Emoji — Windows+Period" entry opens the app when it **injects** Win+. —
  Chromium does (Chrome, Edge, Electron apps): `VK_LWIN` then `VK_OEM_PERIOD` with `SendInput`, the
  Windows key released first. An app calling `CoreInputView.TryShow` sends no key: no hook can see
  it, Windows' panel opens.
- The hook runs on **its own thread**, with its own message loop, and does nothing but recognise the
  keys and post `Pressed` to the UI thread: every key typed in any app waits on the hook, and Windows
  silently removes one too slow to answer (`LowLevelHooksTimeout`). Never on the UI thread.
- Swallowing `;` or `.` leaves the Windows key going down then up with nothing between — Windows opens the
  Start menu. The hook injects a **dummy key** right away (`0xE8`, unassigned), marked in
  `dwExtraInfo` so it lets it through; being the last app to send input also lets the app take the
  foreground.
- **During a keep-open insertion** (see *Keep-Open Insertion*), from `SwallowKeys` to
  `StopSwallowing` — one second at most —, the hook swallows every physical key-down **but the
  modifiers**: the previous window has the keyboard, and a quick second Ctrl+Enter or Enter's
  auto-repeat would land there. Those keys are lost. A key-up is swallowed only when its key-down was:
  one whose key-down went through must reach Windows, or the key would stay down.
- **Placement**: `Input/CaretLocator.cs` looks for the text cursor of the previous window — the
  Win32 caret, the MSAA caret, the UI Automation caret range, the UI Automation focused element (of
  that window's app only) — else takes the mouse pointer. The sources asking the other app run off
  the UI thread, **200 ms** in all. `UI/WindowPlacement.cs` puts the visible frame under the found
  rectangle, above it when there is no room, inside the monitor's working area. The tray icon's
  click places it in the corner instead (see *Window and Tray Icon* § *Corner*).
- UI Automation and MSAA are declared by hand (`Input/AccessibilityInterop.cs`), like Direct2D: no
  new dependency.
- An agent checking Win+; or Win+. from a script sends it with `SendInput` — injected keys go
  through the hook: the Windows key, then the layout's `;` key or `VK_OEM_PERIOD`. The `;` key depends
  on the layout of the window in front: a check of another layout activates it there
  (`ActivateKeyboardLayout`, `KLF_SETFORPROCESS`) once that window is in front — the layout may be
  global, and comes back as soon as the window loses the front — and restores the user's at the end.

## Categories and Insertion

- **Categories** come from `Data/EmojiCatalog.cs` only — the frequent and custom tabs aside (see *Frequent Tab*, *Custom Tab*): seven tabs in the Win+; order, the Emojibase
  data of `emoji-data\` next to the exe (see *Emoji Data* below). Left out:
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
  A Ctrl key held is released before the characters and pressed again after them, in the same call
  (`EmojiInserter.Type`): the window sees plain characters, never a Ctrl shortcut. Every injected key
  carries `ShortcutHook.InjectedMarker`.

### Keep-Open Insertion

**Ctrl+Enter** and **Ctrl+click** — Ctrl alone — insert the emoji as Enter and a click do, its use
counted, but the window **stays open** (`MainForm.InsertKeepingOpen`): several emojis in a row, or the
same one several times, without reopening it.

- **Sequence**: the window made topmost (`HWND_TOPMOST`, no activation), so the previous window
  brought to the front never covers it; the hook swallowing (see *Shortcut*); the previous window
  activated, the emoji typed; then, `KeepOpenHandBackDelay` (50 ms) later — `SendInput` is
  asynchronous, the characters must reach the previous window first —, `HandForegroundBack`: the
  foreground taken back (`TakeForeground`), the topmost band left (`HWND_NOTOPMOST`), the hook stops
  swallowing.
- **Kept**: the keyboard where it was (search box or grid: the activation restores the focused
  control), the selection on the emoji, the search text. No show, so no `OnVisibleChanged` reset.
- The previous window stays the target: `ForegroundTracker` skips the app's own windows.
- One started while a hand-back is pending is **ignored**. No previous window → nothing typed, the
  window stays, the use counted.
- The frequent section is rebuilt **at once**, the selection kept on its emoji (see *Frequent Tab*).
- An agent cannot check it with posted messages: Ctrl is read from the keyboard's state. Real input
  (`SendInput`) goes to whatever window is in front — another session's instance, which hooks Win+.
  too, or the user's own apps, where Ctrl+Enter may send a message: on a desktop in use, it is checked
  by hand.

## Emoji Data

The **emoji data** — the list, the names, the keywords — is Emojibase's, read at launch from
**`emoji-data\` next to the exe** (`Data/EmojiDataFolder.cs`), not from `cache\`: that folder is
disposable. The embedded copy (`Data/Emojibase/`, [CONTRIBUTING.md § Emoji data](CONTRIBUTING.md#emoji-data))
recreates it.

- **A set of four files**, always written together: `compact.en.json` (the list, English keywords),
  `compact.fr.json` (French keywords, joined by hexcode), `LICENSE` (Emojibase's, MIT) and
  `version.txt` (the set's Emojibase version, `major.minor.patch`). Each one written through
  `<name>.tmp` then a replace, `version.txt` last.
- **At launch** (`EmojiDataFolder.Load`, in place of the embedded resources):

  | The folder | Does |
  |---|---|
  | Missing, or a file of the set missing | The whole set written from the embedded copy, then used |
  | A JSON not parsing into a non-empty entry list (the English one with emojis in the seven tabs), a `version.txt` not a version | Rewritten from the embedded copy, then used |
  | Valid, older than the embedded copy (the exe was updated) | Rewritten from the embedded copy, then used |
  | Valid, same version or newer | Used as it is |
  | Cannot be written | The embedded copy used in memory — never an error |

- "Valid" is checked by **parsing** (`EmojiDataFolder.Parse`): the parsed set is the one used, the
  files are read once. `EmojiCatalog` keeps the parsing of an entry and the building of the
  categories; an emoji given twice keeps its first entry.
- **A new list** changes the pre-render cache's key (the emoji list is in it): rendered again, no
  `FormatVersion` bump. Counters, custom groups and the tray emoji are kept by the emoji's text: an
  emoji the new list no longer has stays in their files and is not shown.
- **`Check for emoji updates…`**, in the settings menu's *app* section, after `Start with Windows`
  (`MainForm.CheckEmojiUpdatesAsync`, `Data/EmojiDataUpdate.cs`): the app's **only network access**,
  on that click only — no check at launch, no setting. The item is greyed while it runs; one
  `HttpClient`, 15 s timeout.
  1. The latest version: `https://registry.npmjs.org/emojibase-data/latest`'s `version` (never a
     pre-release).
  2. Not newer than the version in use — or the one already downloaded this run → `Emoji data is up to
     date`. A network, HTTP or parse failure → `Could not check for emoji updates`, a warning.
  3. Newer → `Update the emoji list, names and keywords?`, Yes / No, *Yes* the default.
  4. Yes → `en/compact.json`, `fr/compact.json` and `LICENSE` from
     `https://cdn.jsdelivr.net/npm/emojibase-data@<version>/`, parsed like the folder (any newer
     major accepted when it parses), then written as a set. Any failure → `Could not update the emoji
     data`, the folder left as it was.
  5. `Restart now to use it?`, Yes / No, *Yes* the default: Yes → the exe started again
     (`MainForm.Restart`), the same arguments, `--title` included, `--background` left out — the
     single instance released first (see *Start with Windows*); No → used at the next launch. A
     failed restart → `Could not restart the app`, a warning, the app kept running.
- An agent checking the update cannot click the settings button from a script (custom-drawn, no
  accessibility): the network and the validation are checked by calling `EmojiDataUpdate` and
  `EmojiDataFolder.Parse` by reflection on the built dll.

## Reset All Settings

**`Reset all settings…`**, last in the settings menu, in its *app* section after `Check for emoji
updates…` (`MainForm.ResetAllSettings`, `Data/AppReset.cs`), puts the app back to its first launch.
Always enabled.

- **Confirmation**: Yes / No, **No the default**, the warning icon, listing what is lost — the custom
  groups, the counters, the window size and opacity, the tray emoji and the details panel's settings, Start with
  Windows, the downloaded emoji data (back to the **embedded** version, named:
  `EmojiDataFolder.EmbeddedVersion`), the image cache — and saying the app restarts.
- **What is deleted** (`AppReset.DeleteAll`), next to the exe: `settings.json`, `usage.json`,
  `custom-groups.json` and their temporary files (`TemporaryFileName`), the whole `cache\` and
  `emoji-data\` folders; in the Startup folder, the **startup shortcut** and the Task Manager's value —
  **only when the shortcut targets this exe**, disabled or not (`StartupShortcut.DisableForThisExe`): a
  shortcut to another copy of the app is left alone. Nothing else, never the exe's folder itself.
- **Each name comes from its owner's constant** (`FileName`, `TemporaryFileName`, `FolderName`): a
  new file the app writes next to the exe gets its row in `AppReset`, or the reset leaves it behind.
- **Order**:
  1. The disk cache stops being written (`EmojiGrid.StopCacheWriting` → `EmojiBitmapCache.StopWriting`),
     waiting for a write under way: the pre-render would put an atlas back. The bitmaps stay shown.
  2. Every item deleted, each one attempted even when another failed; a missing one is not an error.
     Any failure → `Some files could not be deleted:`, one line per item with its reason, a warning —
     then the restart anyway.
  3. The restart (`MainForm.Restart`), as after an emoji update; no question. A failed restart →
     `The settings were reset. Start the app again to finish.`, a warning, then the app **exits**:
     kept running, it would write its old settings back from memory on the next action.
- An agent checking the reset cannot click the settings button: the deletion is checked by calling
  `AppReset` by reflection on the dll of a **copy** of the build folder, never the user's own, after
  `AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", <copy>)` — without it the host's folder is the
  one emptied. The startup shortcut then targets the host (`pwsh.exe`), and is deleted at the end.

## Frequent Tab

The first tab, **Frequently used** (a star, `E734`), lists the emojis used most — while shown (see
*Hidden* below). It is one more `EmojiCategory` at the head of the list `MainForm` gives the tab strip
and the grid, built from the counters (`MainForm.CreateFrequentCategory`), not from the catalog.

- **Counters**: `Data/EmojiUsage.cs` alone. Every call of `MainForm.OnEmojiUsed` adds 1 to the
  emoji's count and sets its last use — every way of inserting goes through it.
- **`usage.json`**, next to the exe (`AppContext.BaseDirectory`), **not** in `cache\`: that folder is
  disposable, the counters are not. A JSON object keyed by the emoji's text, `{ "count", "lastUsed" }`
  (UTC) each. Read once at launch — missing or invalid → no counter, not an error. Written after
  each change, through `usage.json.tmp` then a replace. A folder that cannot be written is not an
  error: the counters live in memory until the app ends.
- The emojis are written **as themselves**, not as `\uXXXX` escapes: System.Text.Json escapes every
  character beyond the BMP even with the relaxed encoder, so the escaped surrogate pairs are turned
  back after serializing (`EmojiUsage.ReadableEmojis`).
- An emoji of the file the catalog no longer has stays in the file and is not shown.
- **Order**: the most used first; equal counts → the most recently used first.
- **Limit**: **3 rows** (`MainForm.FrequentRows`), however many emojis they hold at the current
  width — `EmojiGridLayout` cuts a section to `MaxRows × Columns`, computed again at every resize.
- **Use count** under each emoji of the section, in its cell (`EmojiCategory.Captions`): grey text,
  the emoji at the top of the cell; `999+` beyond 999. The other sections show none.
- **Taller cells**: a captioned section's cells are rectangles — as wide as the others, so the
  columns line up and the limit stays `3 × Columns`, taller to hold the count
  (`EmojiGridLayout.Section.RowHeight`).
- **Selection** (see *Keyboard*): its frame follows the cell — a rectangle around the emoji and its
  count. Replacing the section (`EmojiGrid.ReplaceCategory`, after a use, a removal or *Clear*) keeps
  it on its emoji: wherever that one moved in the section, on its cell in another section — the view
  then moving with the section's change of height, so the emoji keeps its place on screen (under the
  mouse for a repeated Ctrl+click). Gone from the section, it goes back to the first emoji in view.
- **Empty**: the tab stays, the section reads `No emoji used yet` (`EmojiCategory.EmptyText`, one row
  kept for it — the mechanism `No emoji found` uses too).
- **Every show** scrolls the grid to the top, on the first section — this one while shown
  (`OnVisibleChanged`).
- The **search box** searches the catalog's categories only: the frequent section would give each of
  its emojis twice.
- The grid pre-renders the catalog's emojis only (`EmojiGrid`'s `emojis` argument): the frequent
  section reuses their bitmaps, and the disk cache's key never changes with the counters.
- **Clear frequently used**, in the settings menu: a Yes / No confirmation, *No* the default, then
  every counter reset and `usage.json` rewritten empty. Greyed while there is no counter.
- **Remove from frequently used**, in the emoji's right-click menu (see *Custom Tab*), on the
  frequent section only — never in search mode, whose results are the grid's first section too
  (`MainForm.IsFrequentSection`). No confirmation: the emoji's counter is **forgotten**
  (`EmojiUsage.Remove`) and `usage.json` written; the next emoji moves up into the section, which
  reads `No emoji used yet` once empty. Used again, the emoji starts over at 1.
- **Hidden**: `Show frequently used` in the settings menu, checked while shown, and `Hide frequently
  used` in the section's **"…" button** — the same setting, saved as `showFrequent` in
  `settings.json` (`SettingsFile.ReadShowFrequent`; missing or unreadable → shown). No confirmation:
  nothing is lost.
  - Hidden, the tab leaves the strip and the section the grid; the counters **keep counting**, and
    *Clear frequently used* stays usable. Shown again, the section is up to date and first.
  - A toggle rebuilds the frequent and custom sections and the tabs (`MainForm.RebuildSections`,
    `CategoryTabStrip.ReplaceTabs`): the grid back at the top on its first emoji — unless a search is
    shown — and the window's `MinimumSize` following the tab count, one tab narrower while hidden.

## Custom Tab

The **Custom** tab (a heart, `EB51`), right after the frequent one, holds the user's **custom
groups**: one section each, under the group's name. While there is no group, it holds one `Custom`
section reading `Create a group from ⚙ → New group…`; while every group is hidden, the tab is gone.

- **One tab, several sections**: the tab strip lists the frequent tab and the custom one while
  shown, then the catalog's (`CategoryTabStrip.ReplaceTabs` when one comes or goes), while the grid's
  custom sections come and go (`EmojiGrid.ReplaceCategories`). `MainForm.TabOf` / `SectionOf` map one
  to the other, `GroupOf` / `SectionOfGroup` a section to its group: a click on the tab scrolls to the first group, and the tab is
  active while any group's section is at the top. A change to the groups keeps the view where it was
  when it is below them (it moves with their change of height).
- **Groups**: `Data/CustomGroups.cs` alone. **`custom-groups.json`**, next to the exe, like
  `usage.json`: a JSON array of `{ "name", "emojis", "hidden" }` (`hidden` written only when set),
  the groups and their emojis in the user's order, the emojis as their text
  (`EmojiUsage.ReadableEmojis`). Read once at launch — missing or invalid → no group; a group without
  a name or an emoji list is left out. Written after each
  change, through `custom-groups.json.tmp` then a replace; a folder that cannot be written keeps the
  groups in memory until the app ends.
- The emojis are the **catalog's texts**, `FE0F` included (`👍️`, `✅️`): a file written by hand
  without it does not match, and the emoji is not shown. An emoji the catalog no longer has stays in
  the file and is not shown. An emoji is at most once in a group, and may be in several groups.
- **New group…** (settings menu): `UI/GroupNameDialog.cs` asks for the name — trimmed, *OK* greyed
  while blank, duplicates allowed, no length limit. The group comes last; the grid scrolls to it
  unless a search is shown.
- **Right click** on an emoji, in any section — search results included: after `Use as tray icon`
  (see *Window and Tray Icon*), `Add to ▸` lists every
  group, the ones holding the emoji **checked**; a click adds it at the end of the group, or takes
  it out when checked. Greyed while there is no group. In a group's section, `Remove` takes it out
  of that group; in the frequent section, `Remove from frequently used` (see *Frequent Tab*). The
  menus are built for one show (`MainForm.ShowOnce`); `MainForm.ShowEmojiMenu` alone builds the
  emoji's, from the mouse and the keyboard alike (see *Keyboard*).
- **"…" button**, at the right end of a section's header when its `EmojiCategory.HasMenu` is set —
  the groups' and the frequent section's (see *Frequent Tab*); the header's name ends before it. A
  group's menu (`MainForm.CreateGroupMenu`): `Rename…`, `Reorder` (greyed under two emojis shown),
  `Move up` / `Move down` (swap with the nearest **shown** group — a hidden one in between keeps its
  place —, greyed at the ends, the group kept in view), `Hide group`, `Delete group` — a Yes / No
  confirmation, *No* the default, when the group holds emojis; an empty one is deleted right away.
- **Hidden group**: `Hide group` in its menu, no confirmation; `Show groups ▸` in the settings menu
  lists every group, checked while shown, a click hiding it or showing it again — greyed while there
  is no group. The flag is saved **with the group** (`CustomGroups.SetHidden`), so a rename keeps it.
  A hidden group has no section, but stays in `Add to ▸`; the custom tab leaves once every group is
  hidden. A toggle goes through `MainForm.RebuildSections`, like the frequent tab's.
- **Reorder mode** (`EmojiGrid.StartReorder`), one section at a time: its "…" becomes `Done`, in the
  accent colour. A press on one of its emojis then a move beyond `SystemInformation.DragSize` drags
  it: hover and selection stay still, a bar in the accent colour shows the gap it goes in
  (`EmojiGridLayout.Insertion`), the release drops it (`EmojiMoved`) and the file is written at once.
  A click or Enter on its emojis inserts nothing; the other sections insert as usual. It ends on
  `Done`, Esc, the window hiding, a search, and a group being moved or deleted.
- The **search box** searches the catalog's categories only: a group would give its emojis twice.
- The grid pre-renders the catalog's emojis only: the groups reuse their bitmaps.

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
- **Every show** clears the box and focuses it (`OnVisibleChanged`), and brings the grid back to the
  top on its first emoji (see *Keyboard*). The placeholder is the native
  cue banner shown while focused (`EM_SETCUEBANNER`): `PlaceholderText` hides on focus, and the box
  always has it.
- **Search mode** (non-blank text): the tab strip is `Greyed` (clicks ignored), the grid shows one
  `Search results` section (`No emoji found` when empty) and raises no `ActiveCategoryChanged`.
  Emptying the box while the window stays open brings the categories back at their previous scroll
  position; a show brings them back at the top.
- The ✕ next to the box is exactly as high as it: showing it must not move the tabs.
- An agent checking the search from a script sends `WM_SETTEXT` / `WM_KEYDOWN` to the box itself,
  never global keystrokes (`SendKeys`, `SendInput`): the window may not be in front, and the keys
  would land in another app.

## Keyboard

One emoji of the grid is the **selection** (`UI/EmojiGrid.cs`), framed in the accent colour
(`SystemColors.Highlight`). The mouse moving over an emoji selects it; the keyboard moves it. Enter
inserts it, wherever the keyboard is.

The keyboard has **two places**: the search box, focused on every show, and the grid, which gets the
focus only from a navigation key in the box — ↓, or any of the grid's while the box is empty — it is
not selectable, a click never focuses it. `MainForm.ProcessCmdKey`
routes the keys; the target cells are computed by `UI/EmojiGridLayout.cs` alone.

| Focus | Key | Does |
|---|---|---|
| Search box, with text | ← / →, Home / End | The text caret |
| Search box, with text | ↓ | The grid gets the keyboard, the selection on its first emoji; no result → nothing, the keyboard stays in the box |
| Search box, with text | Page Up / Page Down, Tab / Shift+Tab | Ignored |
| Search box, empty | ← / →, ↑ / ↓, Home / End, Ctrl+Home / Ctrl+End, Page Up / Page Down, Tab / Shift+Tab | As in the grid, from the selection, and the grid gets the keyboard — even when the selection cannot move. ↑ on the grid's first row → nothing, the keyboard stays in the box |
| Search box | Menu key, Shift+F10 | The box's own menu (Cut, Copy, Paste…) |
| Grid | ← / → | Previous / next emoji, across rows and categories |
| Grid | ↑ / ↓ | One row up / down, same column, across categories; a shorter row → its last emoji. ↑ on the grid's first row → back to the search box |
| Grid | Home / End | First / last emoji of the selection's category |
| Grid | Ctrl+Home / Ctrl+End | First / last emoji of the grid |
| Grid | Page Up / Page Down | As many rows as the viewport holds, stopping on the first / last row |
| Grid | Tab / Shift+Tab | First emoji of the next / previous category, its header at the top; wraps around. Ignored in search mode |
| Grid | A character, Backspace | Back to the search box, the key typed into it |
| Grid | Menu key, Shift+F10 | The selection's right-click menu, under its cell (scrolled into view first), its first enabled item highlighted (`EmojiGrid.OpenSelectionMenu`) |
| Both | Enter | Inserts the selection (nothing when there is none, or when it is in the group in reorder mode) |
| Both | Ctrl+Enter | As Enter, the window kept open, the keyboard staying where it is (see *Keep-Open Insertion*) |
| Both | Esc | Ends the reorder mode; otherwise clears the box; already empty → hides the window |

- **Empty** means no character at all (`TextLength == 0`): a box holding only spaces is *with text*,
  its caret still has somewhere to go. The empty box's keys are the ones `EmojiGrid.MoveSelection`
  answers (`EmojiGrid.IsNavigationKey`); Enter, Esc and the Menu key / Shift+F10 keep their rows.
- **Where the selection goes**: every show → the grid's first emoji, scrolled to the top; every
  change of the search text → the first result; emptying the box → the first emoji in view; a tab
  click or Tab → the category's first emoji. Anything else leaves it alone.
- A keyboard move scrolls the least that shows the selected cell. **Only a real mouse move selects**:
  Windows also sends `WM_MOUSEMOVE` when the content scrolls or the window appears under a still
  cursor, so `EmojiGrid` compares the cursor's screen position with the last one seen — reset on
  every show.
- **No tooltip** on the grid's emojis: the details panel shows the selection's name, whether the
  mouse or the keyboard moved it.
- An agent checking the keys from a script **posts** `WM_KEYDOWN` to the focused control (`SendMessage`
  skips `ProcessCmdKey`); a window that is not in front has no focus — post it `WM_ACTIVATE` first
  rather than taking the foreground. The check instance also answers Win+;: while it runs, the
  user's Win+; may open it.

## Details Panel

Docked at the bottom of the window, under the grid (`UI/EmojiDetailsPanel.cs`), custom-drawn. It shows
the grid's **selection**: `EmojiGrid.SelectedEmojiChanged` tells `MainForm`, raised whenever the
selected emoji changes — the same cell may hold another emoji once the sections change.

- **Left**: the emoji at 48 logical pixels, through the panel's own `EmojiRenderer` (the grid's cache
  renders at the grid's size only); under it its **emoticons** (Emojibase `emoticon`, a string or an
  array), only for the few emojis that have some.
- **Middle**: one **row per language**, a thin line between them — the English row always, the
  French one while `showFrench` (settings menu, `Show French names`; default on). A row: the flag,
  then the name in bold (the French name capitalized like the English one), then **every tag**,
  joined with `, ` and **wrapped** at spaces — never truncated, never an ellipsis. The names of both
  rows line up after the widest flag.
- **Flags**: images embedded in the exe (`UI/Flags/`, `us` and `fr`, 1× and `@2x`, the 2× one above
  144 DPI), drawn at 12 logical pixels high with a thin edge — Segoe UI Emoji has no flag glyphs.
  Generated from the public-domain flag designs, never downloaded.
- **Right**: the **copy button**, the copy glyph only (Segoe Fluent Icons `E8C8`). Its tooltip is the
  emoji's **first code point** (`U+1F602`; a sequence shows its first one). A click copies **the
  emoji itself** to the clipboard as text (`Clipboard.SetText`); the glyph turns into a check mark
  (`E73E`) for about a second. A copy neither hides the window nor counts as a use. A clipboard held
  by another app copies nothing and shows no check mark.
- **Height**: fixed, the one the emoji with the **most text** needs at the window's width, French row
  included while shown (`EmojiDetailsPanel.HeightFor`, over the whole catalog, word widths cached):
  moving the selection never moves the grid. `MainForm.OnLayout` fits it **before** the docking
  places the controls — set during it, the grid would keep the old space. The window's minimum
  height is 240 logical pixels plus the panel's height.
- **Search highlight**: while the search box holds text, the characters it matches in the names and
  tags are highlighted in `highlightColor` (settings menu, `Highlight color…`, which shows it as a
  swatch; `#RRGGBB` in `settings.json`; default fluorescent yellow `#FFFF00`), the text keeping its
  colour. Exactly the
  matched characters, every occurrence, matched as the search matches (`EmojiSearch.MatchSpans`:
  accents and case ignored, inside one word).
- **No selection** (a search with no result): the panel stays, empty, at the same height.
- The panel is not selectable: a click on it leaves the keyboard where it was.

## Repository Docs

The repository's docs — the French versions (`README.fr.md`, `GLOSSARY.fr.md`) kept in step with
the English ones, the language line, one place per fact — follow the rules shared by every
mini-app: `../CLAUDE.md` § Repository Docs.
