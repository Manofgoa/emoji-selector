# Security

Emoji Selector is an offline desktop app: it inserts the emojis you pick into the app you are
typing in. It connects to the network only when you click **Check for emoji updates…** in its
settings menu: it then asks npm's registry (`registry.npmjs.org`) for the latest `emojibase-data`
version and, when you accept the update, downloads its files from jsDelivr (`cdn.jsdelivr.net`).
Nothing is sent but these requests; the downloaded files are checked before they replace the
`emoji-data` folder next to the exe.

Found a vulnerability? Please report it privately, through the repository's **Security** tab →
**Report a vulnerability**, rather than in a public issue. Only the latest version on `main` is
supported.
