using Recap.State;
using Spectre.Console;

namespace Recap.Tui;

public static class ResultPanel
{
    public static void ShowActionMenu(SessionState state)
    {
        if (state.TranscriptText == null) return;

        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold cyan]Transcript[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(Markup.Escape(state.TranscriptText))
            .Border(BoxBorder.Rounded)
            .Expand());

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Action:[/]")
                .AddChoices("Copy to clipboard", "Save as .md", "Save as .txt", "Append to file", "Discard"));

        switch (action)
        {
            case "Copy to clipboard":
                TextCopy.ClipboardService.SetText(state.TranscriptText);
                state.LastActionResult = "Copied to clipboard";
                break;

            case "Save as .md":
                SaveTranscript(state, ".md");
                break;

            case "Save as .txt":
                SaveTranscript(state, ".txt");
                break;

            case "Append to file":
                AppendTranscript(state);
                break;

            case "Discard":
                state.LastActionResult = "Transcript discarded";
                break;
        }

        state.TranscriptText = null;
        state.Mode = Models.AppMode.Normal;
    }

    private static void SaveTranscript(SessionState state, string extension)
    {
        var defaultName = $"transcript-{DateTime.Now:yyyy-MM-dd-HHmm}{extension}";
        var folder = AnsiConsole.Prompt(
            new TextPrompt<string>("[bold]Save folder:[/]")
                .DefaultValue(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)));

        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, defaultName);
        File.WriteAllText(path, state.TranscriptText);
        state.LastActionResult = $"Saved to {path}";
    }

    private static void AppendTranscript(SessionState state)
    {
        var path = AnsiConsole.Prompt(
            new TextPrompt<string>("[bold]File to append to:[/]"));

        File.AppendAllText(path, $"\n\n---\n\n{state.TranscriptText}");
        state.LastActionResult = $"Appended to {path}";
    }
}
