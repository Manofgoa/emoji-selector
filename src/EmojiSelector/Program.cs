using EmojiSelector.UI;

namespace EmojiSelector;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        static bool IsTitle(string arg) => arg.Equals(MainForm.TitleArgument, StringComparison.OrdinalIgnoreCase);
        static bool IsBackground(string arg) => arg.Equals(MainForm.BackgroundArgument, StringComparison.OrdinalIgnoreCase);
        string? secondTitle = null;
        bool background = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (IsTitle(args[i]))
            {
                // Its value is the next argument, unless that one is an option itself. Missing or blank, it is
                // ignored; given twice, the last one wins.
                if (i + 1 < args.Length && !IsTitle(args[i + 1]) && !IsBackground(args[i + 1]))
                {
                    i++;
                    if (!string.IsNullOrWhiteSpace(args[i]))
                    {
                        secondTitle = args[i].Trim();
                    }
                }
            }
            else if (IsBackground(args[i]))
            {
                background = true;
            }
        }

        // Taken before anything is loaded. Another instance of this exe running: a launch by hand shows its window, a
        // sign-in leaves it as it is — this one's arguments are ignored either way.
        using var singleInstance = new SingleInstance();
        if (!singleInstance.IsFirst)
        {
            if (!background)
            {
                singleInstance.ShowFirst();
            }

            return;
        }

        Application.Run(new MainForm(secondTitle, background, singleInstance));
    }
}
