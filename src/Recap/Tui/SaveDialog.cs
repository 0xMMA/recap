using Recap.Audio;
using Recap.State;
using Spectre.Console;

namespace Recap.Tui;

public static class SaveDialog
{
    public static string? Show(SessionState state)
    {
        if (state.Segments.Count == 0)
            return null;

        var mode = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Save as:[/]")
                .AddChoices("Spliced single file", "Individual segments", "Cancel"));

        if (mode == "Cancel") return null;

        var defaultName = $"session-{DateTime.Now:yyyy-MM-dd-HHmm}";
        var folder = AnsiConsole.Prompt(
            new TextPrompt<string>("[bold]Save folder:[/]")
                .DefaultValue(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)));

        Directory.CreateDirectory(folder);

        if (mode == "Spliced single file")
        {
            var fileName = $"{defaultName}.wav";
            var outputPath = GetUniqueFilePath(Path.Combine(folder, fileName));
            AudioEngine.SpliceSegments(state.Segments.Select(s => s.FilePath), outputPath);
            return outputPath;
        }
        else
        {
            var segFolder = Path.Combine(folder, defaultName);
            Directory.CreateDirectory(segFolder);
            foreach (var seg in state.Segments)
            {
                var destPath = Path.Combine(segFolder, $"{seg.Index + 1:D2}.wav");
                File.Copy(seg.FilePath, destPath, true);
            }
            return segFolder;
        }
    }

    private static string GetUniqueFilePath(string basePath)
    {
        if (!File.Exists(basePath)) return basePath;

        var dir = Path.GetDirectoryName(basePath)!;
        var name = Path.GetFileNameWithoutExtension(basePath);
        var ext = Path.GetExtension(basePath);
        int n = 1;

        string path;
        do
        {
            path = Path.Combine(dir, $"{name}-{n:D2}{ext}");
            n++;
        } while (File.Exists(path));

        return path;
    }
}
