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
| Emoji clicked in the grid | Inserted into the **previous window**, then the window hides to the tray (see *Insertion* below); its use counted (see *Frequent Tab* below) |
| Enter, in the search box or the grid | Inserts the **selection**, like a click on it (see *Keyboard* below) |
| Esc, in the search box or the grid | Clears the box; already empty → hides the window to the tray |
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

- **Categories** come from `Data/EmojiCatalog.cs` only — the frequent tab aside (see *Frequent Tab*): seven tabs in the Win+; order, the Emojibase
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

## Frequent Tab

The first tab, **Frequently used** (a star, `E734`), lists the emojis used most. It is one more
`EmojiCategory` at the head of the list `MainForm` gives the tab strip and the grid, built from the
counters (`MainForm.CreateFrequentCategory`), not from the catalog.

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
  count. Replacing the section (`EmojiGrid.ReplaceCategory`, after a use or *Clear*) puts it back on
  the first emoji in view: the cell it was on may be gone.
- **Empty**: the tab stays, the section reads `No emoji used yet` (`EmojiCategory.EmptyText`, one row
  kept for it — the mechanism `No emoji found` uses too).
- **Every show** scrolls the grid to the top, on this section (`OnVisibleChanged`).
- The **search box** searches the catalog's categories only: the frequent section would give each of
  its emojis twice.
- The grid pre-renders the catalog's emojis only (`EmojiGrid`'s `emojis` argument): the frequent
  section reuses their bitmaps, and the disk cache's key never changes with the counters.
- **Clear frequently used**, in the settings menu: a Yes / No confirmation, *No* the default, then
  every counter reset and `usage.json` rewritten empty. Greyed while there is no counter.

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
focus only from ↓ in the box — it is not selectable, a click never focuses it. `MainForm.ProcessCmdKey`
routes the keys; the target cells are computed by `UI/EmojiGridLayout.cs` alone.

| Focus | Key | Does |
|---|---|---|
| Search box | ← / →, Home / End | The text caret |
| Search box | ↓ | The grid gets the keyboard, the selection on its first emoji; no result → nothing, the keyboard stays in the box |
| Search box | Page Up / Page Down, Tab / Shift+Tab | Ignored |
| Grid | ← / → | Previous / next emoji, across rows and categories |
| Grid | ↑ / ↓ | One row up / down, same column, across categories; a shorter row → its last emoji. ↑ on the grid's first row → back to the search box |
| Grid | Home / End | First / last emoji of the selection's category |
| Grid | Ctrl+Home / Ctrl+End | First / last emoji of the grid |
| Grid | Page Up / Page Down | As many rows as the viewport holds, stopping on the first / last row |
| Grid | Tab / Shift+Tab | First emoji of the next / previous category, its header at the top; wraps around. Ignored in search mode |
| Grid | A character, Backspace | Back to the search box, the key typed into it |
| Both | Enter | Inserts the selection (nothing when there is none) |
| Both | Esc | Clears the box; already empty → hides the window |

- **Where the selection goes**: every show → the grid's first emoji, scrolled to the top; every
  change of the search text → the first result; emptying the box → the first emoji in view; a tab
  click or Tab → the category's first emoji. Anything else leaves it alone.
- A keyboard move scrolls the least that shows the selected cell. **Only a real mouse move selects**:
  Windows also sends `WM_MOUSEMOVE` when the content scrolls or the window appears under a still
  cursor, so `EmojiGrid` compares the cursor's screen position with the last one seen — reset on
  every show.
- The tooltip names the emoji under the **mouse** only; a keyboard selection shows no name.
- An agent checking the keys from a script **posts** `WM_KEYDOWN` to the focused control (`SendMessage`
  skips `ProcessCmdKey`); a window that is not in front has no focus — post it `WM_ACTIVATE` first
  rather than taking the foreground. The check instance also answers Win+;: while it runs, the
  user's Win+; may open it.

## Repository Docs

The repository's docs — the French versions (`README.fr.md`, `GLOSSARY.fr.md`) kept in step with
the English ones, the language line, one place per fact — follow the rules shared by every
mini-app: `../CLAUDE.md` § Repository Docs.
