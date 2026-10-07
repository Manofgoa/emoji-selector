# Emoji Selector

App-level agent instructions, on top of the rules shared by every mini-app (`../CLAUDE.md`).

@RULES.md
@GLOSSARY.md

## Launch

Every launch of the app by an agent — the delivery launch of `../CLAUDE.md` § Launch After Delivery
and the agent's own checking launches alike — passes the **session's name** as the second title
(RULES.md § Command-Line Arguments), so instances launched side by side by parallel sessions tell
which implementation each one tests:

```bash
src/EmojiSelector/bin/Debug/net10.0-windows10.0.19041.0/win-x64/EmojiSelector.exe --title "<session name>"
```

- The session's name is read **at launch time** with the session tool on `self`, so a renamed
  session is honoured.
- Its **status marker is removed** (`🏗️`, `✅`, `❓`, `🚦`, `⏳`): it changes while the app runs.
  `🏗️ Global hotkey` → `--title "Global hotkey"`.
- The name cannot be read → launched without `--title`, and the report says so.
- The survival check (`Get-Process EmojiSelector`) finds the instance by its `MainWindowTitle`,
  `Emoji Selector — <session name>`, among the others.
