namespace EmojiSelector.Data;

/// <summary>
/// A <b>skin tone</b>: <see cref="None"/>, the yellow emoji, or one of Unicode's five skin-tone modifiers
/// (<c>1F3FB</c>–<c>1F3FF</c>), in their order.
/// </summary>
internal enum SkinTone
{
    None,
    Light,
    MediumLight,
    Medium,
    MediumDark,
    Dark,
}

/// <summary>
/// One skin-tone variant of an emoji, as Emojibase gives it: its text, and the tone of its first and second person —
/// the same for a one-person emoji. Never <see cref="SkinTone.None"/>.
/// </summary>
internal sealed record SkinVariant(string Text, SkinTone First, SkinTone Second);

/// <summary>A tone per person: the same twice for a one-person emoji; <see cref="SkinTone.None"/> twice is the yellow emoji.</summary>
internal readonly record struct SkinTonePair(SkinTone First, SkinTone Second)
{
    public static SkinTonePair Of(SkinTone tone) => new(tone, tone);
}

/// <summary>
/// The skin tones' names, colours and file keys, and the <b>shown text</b> of an emoji: the variant of the tone in use —
/// its own tone, else the default one —, or the emoji itself.
/// </summary>
internal static class SkinTones
{
    /// <summary>Every tone, <see cref="SkinTone.None"/> first.</summary>
    public static readonly IReadOnlyList<SkinTone> All =
        [SkinTone.None, SkinTone.Light, SkinTone.MediumLight, SkinTone.Medium, SkinTone.MediumDark, SkinTone.Dark];

    /// <summary>The five tones a modifier gives, <see cref="SkinTone.None"/> left out.</summary>
    public static readonly IReadOnlyList<SkinTone> Modifiers = [.. All.Skip(1)];

    // The modifier of SkinTone.Light; the others follow it.
    private const int FirstModifier = 0x1F3FB;

    /// <summary>The tone's key in the files: <c>none</c>, <c>light</c>, <c>medium-light</c>…</summary>
    public static string KeyOf(SkinTone tone) => tone switch
    {
        SkinTone.Light => "light",
        SkinTone.MediumLight => "medium-light",
        SkinTone.Medium => "medium",
        SkinTone.MediumDark => "medium-dark",
        SkinTone.Dark => "dark",
        _ => "none",
    };

    /// <summary>The tone a file key stands for; null when it is not one.</summary>
    public static SkinTone? Parse(string? key)
    {
        foreach (SkinTone tone in All)
        {
            if (KeyOf(tone) == key)
            {
                return tone;
            }
        }

        return null;
    }

    /// <summary>The tone's name in the menus and tooltips: <c>No tone</c>, <c>Light</c>, <c>Medium-light</c>…</summary>
    public static string NameOf(SkinTone tone) => tone switch
    {
        SkinTone.Light => "Light",
        SkinTone.MediumLight => "Medium-light",
        SkinTone.Medium => "Medium",
        SkinTone.MediumDark => "Medium-dark",
        SkinTone.Dark => "Dark",
        _ => "No tone",
    };

    /// <summary>The tone's swatch colour: Fluent Emoji's yellow and skin colours.</summary>
    public static Color ColorOf(SkinTone tone) => tone switch
    {
        SkinTone.Light => Color.FromArgb(0xF7, 0xD7, 0xC4),
        SkinTone.MediumLight => Color.FromArgb(0xD8, 0xB0, 0x94),
        SkinTone.Medium => Color.FromArgb(0xBB, 0x91, 0x67),
        SkinTone.MediumDark => Color.FromArgb(0x8E, 0x56, 0x2E),
        SkinTone.Dark => Color.FromArgb(0x61, 0x3D, 0x30),
        _ => Color.FromArgb(0xFF, 0xC8, 0x3D),
    };

    /// <summary>
    /// The tones a variant's text holds, read from its modifiers in order: one → the same for both persons, two → the
    /// first person's then the second's; null for none or more than two.
    /// </summary>
    public static SkinTonePair? TonesOf(string text)
    {
        var tones = new List<SkinTone>();
        for (int i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
        {
            if (char.IsSurrogate(text, i) && !char.IsSurrogatePair(text, i))
            {
                continue;
            }

            int codePoint = char.ConvertToUtf32(text, i);
            if (codePoint >= FirstModifier && codePoint < FirstModifier + Modifiers.Count)
            {
                tones.Add((SkinTone)(codePoint - FirstModifier + 1));
            }
        }

        return tones.Count switch
        {
            1 => SkinTonePair.Of(tones[0]),
            2 => new SkinTonePair(tones[0], tones[1]),
            _ => null,
        };
    }

    /// <summary>
    /// The text <paramref name="emoji"/> is shown, inserted and copied as: the variant of its own tone when it has one,
    /// else of the default tone — <paramref name="secondDefaultTone"/> for a two-person emoji's second person, null the
    /// same as the first. The emoji itself when it has no variant, when the tone is <see cref="SkinTone.None"/>, or
    /// when no variant holds the tones asked for.
    /// </summary>
    public static string ShownText(Emoji emoji, SkinTonePair? ownTone, SkinTone defaultTone, SkinTone? secondDefaultTone)
    {
        if (emoji.Variants.Count == 0)
        {
            return emoji.Text;
        }

        SkinTonePair tones = ownTone
            ?? new SkinTonePair(defaultTone, emoji.IsTwoPerson ? secondDefaultTone ?? defaultTone : defaultTone);
        if (tones.First == SkinTone.None || tones.Second == SkinTone.None)
        {
            return emoji.Text;
        }

        if (!emoji.IsTwoPerson)
        {
            tones = SkinTonePair.Of(tones.First);
        }

        return emoji.Variants.FirstOrDefault(variant => variant.First == tones.First && variant.Second == tones.Second)?.Text
            ?? emoji.Text;
    }
}
