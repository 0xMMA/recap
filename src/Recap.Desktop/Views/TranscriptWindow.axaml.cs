using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Recap.Core.Logging;

namespace Recap.Desktop.Views;

public partial class TranscriptWindow : Window
{
    private readonly string _text;

    public TranscriptWindow(string transcriptText)
    {
        InitializeComponent();
        _text = transcriptText;
        var textBox = this.FindControl<TextBox>("TranscriptTextBox");
        if (textBox != null)
            textBox.Text = _text;
    }

    private async void Copy_Click(object? sender, RoutedEventArgs e)
    {
        await TextCopy.ClipboardService.SetTextAsync(_text);
        if (sender is Button btn)
        {
            var original = btn.Content;
            btn.Content = "Copied!";
            await Task.Delay(1500);
            btn.Content = original;
        }
    }

    private async void SaveMd_Click(object? sender, RoutedEventArgs e)
    {
        await SaveAs("md");
    }

    private async void SaveTxt_Click(object? sender, RoutedEventArgs e)
    {
        await SaveAs("txt");
    }

    private async void Append_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions { Title = "Append to file", AllowMultiple = false });
        if (files.Count > 0)
        {
            var path = files[0].Path.LocalPath;
            await File.AppendAllTextAsync(path, $"\n\n{_text}");
            Log.Info($"Transcript appended to {path}");
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async Task SaveAs(string ext)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = $"Save transcript as .{ext}",
                SuggestedFileName = $"transcript-{DateTime.Now:yyyy-MM-dd-HHmm}.{ext}",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType($".{ext} files") { Patterns = new[] { $"*.{ext}" } }
                }
            });
        if (file != null)
        {
            await File.WriteAllTextAsync(file.Path.LocalPath, _text);
            Log.Info($"Transcript saved to {file.Path.LocalPath}");
        }
    }
}
