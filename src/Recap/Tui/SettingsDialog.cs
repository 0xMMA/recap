using Recap.Config;
using Spectre.Console;

namespace Recap.Tui;

public static class SettingsDialog
{
    public static void Show(AppConfig config)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold cyan]Settings[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        config.ApiKey = AnsiConsole.Prompt(
            new TextPrompt<string>("[bold]ElevenLabs API key:[/]")
                .DefaultValue(config.ApiKey)
                .Secret('*'));

        config.DefaultLanguage = AnsiConsole.Prompt(
            new TextPrompt<string>("[bold]Default language code[/] (e.g. de, en, auto):")
                .DefaultValue(config.DefaultLanguage));

        config.AudioEvents = AnsiConsole.Confirm("Tag audio events in transcripts?", config.AudioEvents);

        config.TimestampGranularity = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Timestamp granularity:[/]")
                .AddChoices("none", "segment", "word")
                .HighlightStyle(new Style(Color.Cyan1)));

        config.PushToTalk = AnsiConsole.Confirm("Push-to-talk mode (hold R)?", config.PushToTalk);

        config.Save();
        AnsiConsole.MarkupLine("[green]Settings saved.[/]");
        Thread.Sleep(800);
    }

    public static bool RunFirstRunWizard(AppConfig config)
    {
        if (!string.IsNullOrEmpty(config.ApiKey))
            return false;

        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("Recap").Color(Color.Cyan1));
        AnsiConsole.MarkupLine("[bold]First-time setup[/]\n");

        config.ApiKey = AnsiConsole.Prompt(
            new TextPrompt<string>("[bold]ElevenLabs API key:[/]")
                .PromptStyle("green")
                .Secret('*'));

        config.DefaultLanguage = AnsiConsole.Prompt(
            new TextPrompt<string>("[bold]Default language code[/] (e.g. de, en):")
                .DefaultValue("de"));

        config.PushToTalk = AnsiConsole.Confirm("Push-to-talk mode (hold R to record)?", false);

        config.Save();
        AnsiConsole.MarkupLine("\n[green]Setup complete! Press any key to start.[/]");
        Console.ReadKey(true);
        return true;
    }
}
