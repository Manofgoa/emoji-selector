namespace EmojiSelector.Data;

/// <summary>
/// The <b>characters</b> an emoji shows, as one types them: <c>1</c> for 1️⃣, <c>?</c> for ❓, <c>OK</c> for 🆗. Typed
/// in the search box, one of them ranks the emoji first (see <see cref="EmojiSearch"/>); the details panel shows them
/// among the tags.
/// </summary>
/// <remarks>
/// Written by hand: Unicode's compatibility form gives only a few of them (<c>!!</c>, <c>TM</c>, the Japanese
/// ideographs), and Emojibase lacks some (<c>=</c> for 🟰, <c>$</c> for 💲). The emojis are keyed without their
/// variation selector (<c>FE0F</c>), which the catalog's texts may or may not carry. An emoji the catalog does not have
/// is ignored.
/// </remarks>
internal static class EmojiCharacters
{
    private const string VariationSelector = "️";

    private static readonly Dictionary<string, string[]> ByEmoji = new (string Emoji, string[] Characters)[]
    {
        // Keycaps: the character, then the combining enclosing keycap.
        ("#⃣", ["#"]),
        ("*⃣", ["*"]),
        ("0⃣", ["0"]),
        ("1⃣", ["1"]),
        ("2⃣", ["2"]),
        ("3⃣", ["3"]),
        ("4⃣", ["4"]),
        ("5⃣", ["5"]),
        ("6⃣", ["6"]),
        ("7⃣", ["7"]),
        ("8⃣", ["8"]),
        ("9⃣", ["9"]),
        ("🔟", ["10"]),

        // Punctuation and math.
        ("❓", ["?"]),
        ("❔", ["?"]),
        ("❗", ["!"]),
        ("❕", ["!"]),
        ("‼", ["!!"]),
        ("⁉", ["!?"]),
        ("➕", ["+"]),
        ("➖", ["-", "−"]),
        ("✖", ["×", "x"]),
        ("➗", ["÷"]),
        ("🟰", ["="]),
        ("💲", ["$"]),
        ("©", ["©"]),
        ("®", ["®"]),
        ("™", ["™", "TM"]),
        ("✔", ["✓"]),
        ("〰", ["~"]),

        // Letters.
        ("🅰", ["A"]),
        ("🅱", ["B"]),
        ("🅾", ["O"]),
        ("🅿", ["P"]),
        ("🆎", ["AB"]),
        ("ℹ", ["i"]),
        ("Ⓜ", ["M"]),
        ("🆑", ["CL"]),
        ("🆒", ["COOL"]),
        ("🆓", ["FREE"]),
        ("🆔", ["ID"]),
        ("🆕", ["NEW"]),
        ("🆖", ["NG"]),
        ("🆗", ["OK"]),
        ("🆘", ["SOS"]),
        ("🆙", ["UP!"]),
        ("🆚", ["VS"]),
        ("🔠", ["ABCD"]),
        ("🔡", ["abcd"]),
        ("🔤", ["abc"]),

        // Numbers.
        ("💯", ["100"]),
        ("🔞", ["18"]),
        ("🔢", ["1234"]),

        // Japanese buttons: their ideographs.
        ("🈁", ["ココ"]),
        ("🈂", ["サ"]),
        ("🈷", ["月"]),
        ("🈶", ["有"]),
        ("🈯", ["指"]),
        ("🉐", ["得"]),
        ("🈹", ["割"]),
        ("🈚", ["無"]),
        ("🈲", ["禁"]),
        ("🉑", ["可"]),
        ("🈸", ["申"]),
        ("🈴", ["合"]),
        ("🈳", ["空"]),
        ("㊗", ["祝"]),
        ("㊙", ["秘"]),
        ("🈺", ["営"]),
        ("🈵", ["満"]),
    }.ToDictionary(entry => entry.Emoji.Replace(VariationSelector, ""), entry => entry.Characters);

    /// <summary>The characters <paramref name="emoji"/> shows, as written in the list; none for most emojis.</summary>
    public static IReadOnlyList<string> Of(string emoji) =>
        ByEmoji.GetValueOrDefault(emoji.Replace(VariationSelector, "")) ?? [];
}
