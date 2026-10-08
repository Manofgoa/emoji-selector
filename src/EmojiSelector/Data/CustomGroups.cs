using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EmojiSelector.Data;

/// <summary>
/// The user's <b>custom groups</b>, shown in the custom tab: each a name and its emojis in the user's order, saved in
/// <see cref="FileName"/> next to the exe — read at launch, written after each change. A missing or invalid file is no
/// group yet; a folder that cannot be written keeps the groups in memory until the app ends. An emoji is at most once
/// in a group, and may be in several groups.
/// </summary>
internal sealed class CustomGroups
{
    public const string FileName = "custom-groups.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // Emojis the catalog no longer has stay here, so they stay in the file: the caller leaves them out of the grid.
    private readonly List<CustomGroup> groups;

    private CustomGroups(List<CustomGroup> groups)
    {
        this.groups = groups;
    }

    /// <summary>The groups, in the user's order.</summary>
    public IReadOnlyList<CustomGroup> Groups => this.groups;

    private static string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>
    /// The groups saved in <see cref="FileName"/>; none when it is missing or cannot be read. A group without a name
    /// or an emoji list is left out, and an emoji given twice in a group is kept once.
    /// </summary>
    public static CustomGroups Load()
    {
        try
        {
            string json = File.ReadAllText(FilePath);
            List<Entry?>? entries = JsonSerializer.Deserialize<List<Entry?>>(json, JsonOptions);
            List<CustomGroup> groups = (entries ?? [])
                .Where(entry => !string.IsNullOrWhiteSpace(entry?.Name) && entry.Emojis is not null)
                .Select(entry => new CustomGroup(entry!.Name!,
                    entry.Emojis!.Where(emoji => !string.IsNullOrEmpty(emoji)).Select(emoji => emoji!).Distinct().ToList(),
                    entry.Hidden ?? false))
                .ToList();
            return new CustomGroups(groups);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new CustomGroups([]);
        }
    }

    /// <summary>A new empty group named <paramref name="name"/>, last, saved.</summary>
    public void Add(string name)
    {
        this.groups.Add(new CustomGroup(name, []));
        this.Save();
    }

    /// <summary>Renames <paramref name="group"/>, saved.</summary>
    public void Rename(int group, string name)
    {
        this.groups[group] = this.groups[group] with { Name = name };
        this.Save();
    }

    /// <summary>Hides <paramref name="group"/>, or shows it again, saved.</summary>
    public void SetHidden(int group, bool hidden)
    {
        this.groups[group] = this.groups[group] with { Hidden = hidden };
        this.Save();
    }

    /// <summary>Deletes <paramref name="group"/> and its list of emojis, saved.</summary>
    public void Delete(int group)
    {
        this.groups.RemoveAt(group);
        this.Save();
    }

    /// <summary>Swaps two groups, saved.</summary>
    public void Swap(int group, int other)
    {
        (this.groups[group], this.groups[other]) = (this.groups[other], this.groups[group]);
        this.Save();
    }

    /// <summary>Whether <paramref name="group"/> holds <paramref name="emoji"/>.</summary>
    public bool Contains(int group, string emoji) => this.groups[group].Emojis.Contains(emoji);

    /// <summary>Adds <paramref name="emoji"/> at the end of <paramref name="group"/>, unless it is there already; saved.</summary>
    public void AddEmoji(int group, string emoji)
    {
        if (!this.Contains(group, emoji))
        {
            this.groups[group] = this.groups[group] with { Emojis = [.. this.groups[group].Emojis, emoji] };
            this.Save();
        }
    }

    /// <summary>Takes <paramref name="emoji"/> out of <paramref name="group"/>, saved.</summary>
    public void RemoveEmoji(int group, string emoji)
    {
        this.groups[group] = this.groups[group] with { Emojis = this.groups[group].Emojis.Where(text => text != emoji).ToList() };
        this.Save();
    }

    /// <summary>
    /// Moves <paramref name="emoji"/> just before <paramref name="before"/> in <paramref name="group"/> — to its end when
    /// <paramref name="before"/> is null — saved.
    /// </summary>
    public void MoveEmoji(int group, string emoji, string? before)
    {
        List<string> emojis = this.groups[group].Emojis.Where(text => text != emoji).ToList();
        int index = before is null ? -1 : emojis.IndexOf(before);
        emojis.Insert(index < 0 ? emojis.Count : index, emoji);
        this.groups[group] = this.groups[group] with { Emojis = emojis };
        this.Save();
    }

    // The whole file, through a temporary one then a replace: a crash never leaves it half-written.
    private void Save()
    {
        string temporary = FilePath + ".tmp";
        try
        {
            File.WriteAllText(temporary, EmojiUsage.ReadableEmojis(JsonSerializer.Serialize(this.groups, JsonOptions)));
            File.Move(temporary, FilePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Not an error: the groups live in memory until the app ends.
        }
    }

    // A group as read from the file: anything may be missing.
    private sealed record Entry(string? Name, List<string?>? Emojis, bool? Hidden);
}

/// <summary>
/// A <b>custom group</b>: its name, its emojis' texts in the user's order, and whether the user hid it — a hidden group
/// has no section in the grid, and is written without the key while shown.
/// </summary>
internal sealed record CustomGroup(string Name, IReadOnlyList<string> Emojis,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Hidden = false);
