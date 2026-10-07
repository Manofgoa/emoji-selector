<img src="https://flagcdn.com/w20/gb.png" width="20" alt="GB"> English · [<img src="https://flagcdn.com/w20/fr.png" width="20" alt="FR"> Français](README.fr.md)

# Emoji Selector

A tiny, fast-starting Windows desktop app that opens on a global keyboard shortcut, finds an emoji by keyword and pastes it into the app you were typing in.

Each word used here has one meaning, given in the [Glossary](GLOSSARY.md).

## Features

- Fast-starting `.exe` with a GUI, no installer
- **Second title**: `EmojiSelector.exe --title "Global hotkey"` opens a window titled *Emoji Selector — Global hotkey* — in its title bar, its taskbar button, Alt+Tab and its tray icon's tooltip — to tell instances running side by side apart (Claude Code passes the name of its session, see `CLAUDE.md`). The value is the argument right after `--title`; missing or blank, it is ignored; given twice, the last one wins. It is never remembered.
- **Tray icon**: as long as it runs, the app lives in the notification area, its icon a colour smiley 😊. A left click shows the window, brings it to the front if other windows cover it, or hides it if it is already in front; a right click opens a menu whose **Exit** ends the app. The window's close (✕, Alt+F4) and minimize buttons hide it to the tray: the app keeps running.

## Planned

- A global keyboard shortcut opening the window from any app.
- A search box finding emojis by keyword, as you type.
- The picked emoji pasted into the app that was active, the window hidden again.
- Favorites and recent emojis shown first; categories to browse without typing.
- The tray icon showing the last emoji used, back to the smiley at the next launch.

## Build & run

See [CONTRIBUTING.md § Build](CONTRIBUTING.md#build).

## Tech

C# / WinForms on .NET 10, using Windows' own components only, no third-party library.

## License

[MIT](LICENSE) © Manofgoa. Contributions: see [CONTRIBUTING.md](CONTRIBUTING.md); security issues: see [SECURITY.md](SECURITY.md).
