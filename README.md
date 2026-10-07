<img src="https://flagcdn.com/w20/gb.png" width="20" alt="GB"> English · [<img src="https://flagcdn.com/w20/fr.png" width="20" alt="FR"> Français](README.fr.md)

# Emoji Selector

A tiny, fast-starting Windows desktop app that opens on a global keyboard shortcut, finds an emoji by keyword and pastes it into the app you were typing in.

Each word used here has one meaning, given in the [Glossary](GLOSSARY.md).

## Features

- Fast-starting `.exe` with a GUI, no installer
- **Second title**: `EmojiSelector.exe --title "Global hotkey"` opens a window titled *Emoji Selector — Global hotkey* — in its title bar, its taskbar button, Alt+Tab and its tray icon's tooltip — to tell instances running side by side apart (Claude Code passes the name of its session, see `CLAUDE.md`). The value is the argument right after `--title`; missing or blank, it is ignored; given twice, the last one wins. It is never remembered.
- **Tray icon**: as long as it runs, the app lives in the notification area, its icon a colour smiley 😊. A left click shows the window, brings it to the front if other windows cover it, or hides it if it is already in front; a right click opens a menu whose **Exit** ends the app. The window's close (✕, Alt+F4) and minimize buttons hide it to the tray: the app keeps running.
- **Categories**: the window shows every emoji, in colour, in one continuous scrolling grid — one section per category, under a strip of tabs in the Win+; panel's order: Smileys & People, Animals & Nature, Food & Drink, Activities, Travel & Places, Objects, Symbols. Clicking a tab jumps to its section; scrolling moves the active tab along. Hovering an emoji shows its name. No flags (Windows' emoji font has none) and no skin tones yet; an emoji newer than Windows' font shows as a box.
- **Insertion**: clicking an emoji types it into the window you were in before, then the window hides to the tray, whose icon now shows that emoji — back to the smiley at the next launch. Windows blocks it into an app run as administrator.

## Planned

- A global keyboard shortcut opening the window from any app.
- A search box finding emojis by keyword, as you type.
- Favorites and recent emojis shown first.
- Skin tones, flags, copying an emoji as text or as an image, keyboard navigation in the grid.

## Build & run

See [CONTRIBUTING.md § Build](CONTRIBUTING.md#build).

## Tech

C# / WinForms on .NET 10, using Windows' own components only, no third-party library. The emoji list comes from [Emojibase](https://emojibase.dev)'s data (MIT), embedded in the exe: see [CONTRIBUTING.md § Emoji data](CONTRIBUTING.md#emoji-data).

## License

[MIT](LICENSE) © Manofgoa. Contributions: see [CONTRIBUTING.md](CONTRIBUTING.md); security issues: see [SECURITY.md](SECURITY.md).
