<img src="https://flagcdn.com/w20/gb.png" width="20" alt="GB"> English · [<img src="https://flagcdn.com/w20/fr.png" width="20" alt="FR"> Français](README.fr.md)

# Emoji Selector

A tiny, fast-starting Windows desktop app that opens on a global keyboard shortcut, finds an emoji by keyword and pastes it into the app you were typing in.

Each word used here has one meaning, given in the [Glossary](GLOSSARY.md).

## Features

- Fast-starting `.exe` with a GUI, no installer
- **Second title**: `EmojiSelector.exe --title "Global hotkey"` opens a window titled *Emoji Selector — Global hotkey* — in its taskbar button, Alt+Tab and its tray icon's tooltip — to tell instances running side by side apart (Claude Code passes the name of its session, see `CLAUDE.md`). The value is the argument right after `--title`; missing or blank, it is ignored; given twice, the last one wins. It is never remembered.
- **Tray icon**: as long as it runs, the app lives in the notification area, its icon a colour smiley 🙂. Right-clicking any emoji — search results included — and choosing **Use as tray icon** puts that emoji in its place, checked in the menu while it is the icon; the choice is kept in `settings.json` next to the exe. Using an emoji never changes the icon. A left click shows the window, brings it to the front if other windows cover it, or hides it if it is already in front; a right click opens a menu whose **Exit** ends the app. The window's close cross (✕) and Alt+F4 hide it to the tray: the app keeps running.
- **Window**: no title bar, like Windows' emoji panel. It opens 16 emojis wide and 8 rows high, at any display scale. The empty band right of the tabs moves the window, its edges resize it — the size you give it comes back at the next launch, kept in a `settings.json` file next to the exe; it is never minimized nor maximized. Right of that band, the gear opens a menu whose **Open app folder** shows the exe in the File Explorer, **New group…** creates a custom group, **Reset window size** brings the window back to its default size and **Clear frequently used** resets the frequent emojis, and the cross (✕) closes the window.
- **Shortcut**: while the app runs, **Win+;** opens its window just under the text cursor of the app you are typing in — or under the focused field, or at the mouse pointer when that app does not tell where its cursor is — and above it when there is no room below. Pressed again with the window in front, it hides it and you are back where you were typing. It takes the place of Windows' own emoji panel: when the app is not running, Win+; opens Windows' panel as usual, and Win+. always does. On an AZERTY keyboard, Win+; is the `; .` key. In an app run as administrator, Windows' panel opens instead.
- **Categories**: the window shows every emoji, in colour, in one continuous scrolling grid — one section per category, under a strip of tabs in the Win+; panel's order: Smileys & People, Animals & Nature, Food & Drink, Activities, Travel & Places, Objects, Symbols. Clicking a tab jumps to its section; scrolling moves the active tab along. Hovering an emoji shows its name. No flags (Windows' emoji font has none) and no skin tones yet; an emoji newer than Windows' font shows as a box.
- **Instant display**: the emojis are drawn in the background as soon as the app starts, so the grid never waits for them, the first scroll included; an emoji not drawn yet shows as a fluorescent green square for a moment. They are kept in a `cache` folder next to the exe, so the next launches have them all at once. Deleting that folder is safe: they are drawn again. An exe in a folder it cannot write to (e.g. `Program Files`) simply draws them at every launch.
- **Frequently used**: the first tab, a star, shows the emojis you use most — the most used first, the most recent first between equals, with each one's number of uses under it. It holds three rows, however many emojis they hold at the window's width, and the window always opens on it. Every use adds 1 to the emoji's counter, kept in a `usage.json` file next to the exe; **Clear frequently used**, in the gear's menu, resets them after a confirmation. Before any use, the tab reads *No emoji used yet*.
- **Custom groups**: the second tab, a heart, holds your own lists of emojis, one section each under its name. **New group…**, in the gear's menu, creates one, last; before the first one, the tab tells you so. Right-clicking any emoji — search results included — opens **Add to**, listing your groups, the ones already holding it checked: clicking one adds the emoji, or takes it out when checked; in a group, **Remove** takes it out too. The **…** at the right of a group's header opens its menu: **Rename…**, **Reorder** — drag the group's emojis to their place, then **Done** or Esc (meanwhile, a click or Enter on them inserts nothing) — **Move up** / **Move down** among the groups, and **Delete group**, after a confirmation when it holds emojis. The groups are kept in a `custom-groups.json` file next to the exe; the search box does not search them, so no emoji is found twice.
- **Insertion**: clicking an emoji types it into the window you were in before, then the window hides to the tray. Windows blocks it into an app run as administrator.
- **Search box**: at the top of the window, empty and ready for typing every time the window opens. Typing a keyword — an emoji's name or one of its tags, in English or in French, case and accents ignored — greys the tabs and shows the matching emojis alone, the most relevant first: the whole word before its start, before a word that only contains it, the most of the word covered first, a name before a tag (`caca` finds 💩 before 🥜 *cacahuète*). **Enter** inserts the first one; **Esc** clears the box, or hides the window when it is already empty. Emptying the box brings the categories back where they were.
- **Keyboard**: one emoji is always selected, framed in the accent colour — the first one when the window opens, the first result while searching, the one under the mouse when it moves. **Enter** inserts it. In the search box, **↓** moves into the grid (it stays in the box when nothing is found); there, the **arrows** move the selection across rows and categories, **Home** / **End** go to the first / last emoji of the category (**Ctrl+Home** / **Ctrl+End**: of the grid), **Page Up** / **Page Down** move a screen, **Tab** / **Shift+Tab** jump to the next / previous category. **↑** on the first row, or typing a letter, goes back to the search box.

## Planned

- Skin tones, flags, copying an emoji as text or as an image.

## Build & run

See [CONTRIBUTING.md § Build](CONTRIBUTING.md#build).

## Tech

C# / WinForms on .NET 10, using Windows' own components only, no third-party library. The emoji list and its keywords, in English and French, come from [Emojibase](https://emojibase.dev)'s data (MIT), embedded in the exe: see [CONTRIBUTING.md § Emoji data](CONTRIBUTING.md#emoji-data).

## License

[MIT](LICENSE) © Manofgoa. Contributions: see [CONTRIBUTING.md](CONTRIBUTING.md); security issues: see [SECURITY.md](SECURITY.md).
