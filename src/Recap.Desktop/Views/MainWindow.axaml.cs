using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Recap.Core.Audio;
using Recap.Core.Config;
using Recap.Core.Logging;
using Recap.Core.Models;
using Recap.Desktop.Controls;
using Recap.Desktop.ViewModels;

namespace Recap.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm;
    private readonly DispatcherTimer _waveformTimer;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainWindowViewModel();
        DataContext = _vm;
        _vm.RecoverSession();
        Closing += (_, _) => _vm.Cleanup();
        KeyDown += OnKeyDown;

        var settingsBtn = this.FindControl<Button>("SettingsButton");
        if (settingsBtn != null)
            settingsBtn.Click += (_, _) => OpenSettings();

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.TranscriptText) && _vm.TranscriptText != null)
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    var window = new TranscriptWindow(_vm.TranscriptText);
                    await window.ShowDialog(this);
                    _vm.TranscriptText = null;
                });
            }
        };

        _waveformTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(66) };
        _waveformTimer.Tick += (_, _) =>
        {
            if (_vm.RecordingState == RecordingState.Recording)
            {
                var waveformControl = this.FindControl<WaveformControl>("Waveform");
                if (waveformControl != null)
                {
                    var width = (int)Math.Max(100, waveformControl.Bounds.Width);
                    _vm.WaveformPeaks = _vm.GetLiveWaveform(width);
                }
            }
        };
        _waveformTimer.Start();
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        switch (e.Key)
        {
            case Key.R when !ctrl:
                _vm.ToggleRecordingCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Up when shift:
                _vm.ExtendSelection(-1);
                e.Handled = true;
                break;
            case Key.Down when shift:
                _vm.ExtendSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                _vm.MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Down:
                _vm.MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Delete:
            case Key.Back:
                _vm.DeleteSelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Z when ctrl:
                _vm.UndoCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.A when ctrl:
                _vm.SelectAllCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Return when shift:
                _vm.PlayAllCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Return:
                _vm.PlaySelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Space when _vm.RecordingState != RecordingState.Recording:
                _vm.TogglePauseCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.X when shift:
                await _vm.TranscribeAllCommand.ExecuteAsync(null);
                e.Handled = true;
                break;
            case Key.X:
                await _vm.TranscribeCommand.ExecuteAsync(null);
                e.Handled = true;
                break;
            case Key.OemComma when ctrl:
                OpenSettings();
                e.Handled = true;
                break;
            case Key.S:
                SaveAudio();
                e.Handled = true;
                break;
            case Key.L:
                _vm.CycleLanguageCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F:
                _vm.OpenInExplorerCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.N:
                _vm.NewSessionCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Q:
                Close();
                e.Handled = true;
                break;
        }
    }

    private async void OpenSettings()
    {
        var config = AppConfig.Load();
        var settingsVm = new SettingsViewModel(config);
        var dialog = new SettingsWindow(settingsVm);
        var result = await dialog.ShowDialog<bool?>(this);
        if (result == true)
        {
            var newConfig = settingsVm.ToConfig();
            newConfig.Save();
            _vm.UpdateConfig(newConfig);
        }
    }

    private async void SaveAudio()
    {
        var segments = _vm.Segments.Where(s => s.HasFile).ToList();
        if (segments.Count == 0)
        {
            _vm.StatusText = "No segments to save";
            return;
        }

        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Save audio",
                SuggestedFileName = $"recap-{DateTime.Now:yyyy-MM-dd-HHmm}.wav",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("WAV files") { Patterns = new[] { "*.wav" } }
                }
            });

        if (file == null) return;

        try
        {
            var filePaths = segments.Select(s => s.FilePath).ToList();
            if (filePaths.Count == 1)
            {
                File.Copy(filePaths[0], file.Path.LocalPath, overwrite: true);
            }
            else
            {
                AudioEngine.SpliceSegments(filePaths, file.Path.LocalPath);
            }
            _vm.StatusText = $"Saved to {file.Path.LocalPath}";
            Log.Info($"Audio saved to {file.Path.LocalPath}");
        }
        catch (Exception ex)
        {
            _vm.StatusText = $"Save failed: {ex.Message}";
            Log.Error("Save audio failed", ex);
        }
    }
}
