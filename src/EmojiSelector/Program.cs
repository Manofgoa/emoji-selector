using EmojiSelector.UI;

namespace EmojiSelector;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        static bool IsTitle(string arg) => arg.Equals(MainForm.TitleArgument, StringComparison.OrdinalIgnoreCase);
        string? secondTitle = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (IsTitle(args[i]))
            {
                // Its value is the next argument, unless that one is an option itself. Missing or blank, it is
                // ignored; given twice, the last one wins.
                if (i + 1 < args.Length && !IsTitle(args[i + 1]))
                {
                    i++;
                    if (!string.IsNullOrWhiteSpace(args[i]))
                    {
                        secondTitle = args[i].Trim();
                    }
                }
            }
        }

        Application.Run(new MainForm(secondTitle));
    }
}
