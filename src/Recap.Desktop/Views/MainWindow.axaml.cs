using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Recap.Core.Audio;
using Recap.Core.Config;
using Recap.Core.Logging;
using Recap.Core.Models;
using Recap.Core.Updates;
using Recap.Desktop.Controls;
using Recap.Desktop.ViewModels;

namespace Recap.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm;
    private readonly DispatcherTimer _waveformTimer;
    private bool _rKeyHeld;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainWindowViewModel();
        DataContext = _vm;
        Closing += (_, _) => _vm.Cleanup();
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;

        var settingsBtn = this.FindControl<Button>("SettingsButton");
        if (settingsBtn != null)
            settingsBtn.Click += (_, _) => OpenSettings();

        var saveBtn = this.FindControl<Button>("SaveButton");
        if (saveBtn != null)
            saveBtn.Click += (_, _) => SaveAudio();

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
            else if (e.PropertyName == nameof(MainWindowViewModel.SelectedSegment))
            {
                Dispatcher.UIThread.Post(SyncListBoxSelection);
            }
        };

        var versionButton = this.FindControl<Button>("VersionButton");
        if (versionButton != null)
        {
            var version = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "dev";
            versionButton.Content = $"v{version}";
        }

        _waveformTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // ~60fps for smooth playback indicator
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
            else
            {
                _vm.UpdatePlaybackPosition();
                _vm.UpdatePlayingSegment();
                foreach (var seg in _vm.Segments)
                    seg.IsPlaying = seg.Model.Index == _vm.PlayingSegmentIndex;
            }
        };
        _waveformTimer.Start();
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // Crash recovery — auto-loads previous session segments
        _vm.RecoverSession();

        // First-run: force settings if no API key
        if (!_vm.HasApiKey)
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

        // Background update check
        _ = CheckForUpdateOnStartupAsync();
    }

    private async Task CheckForUpdateOnStartupAsync()
    {
        try
        {
            var updater = CreateUpdater();
            var (available, version) = await updater.CheckForUpdateAsync();
            if (available)
            {
                _vm.StatusText = $"Update available: v{version} — click version or press U";
                var versionButton = this.FindControl<Button>("VersionButton");
                if (versionButton != null)
                {
                    versionButton.Content = $"v{updater.CurrentVersion} \u2192 v{version}";
                    versionButton.Foreground = new SolidColorBrush(Color.Parse("#4ecdc4"));
                }
            }
        }
        catch { } // Silent on startup
    }

    private async void CheckForUpdate()
    {
        _vm.StatusText = "Checking for updates...";
        var updater = CreateUpdater();
        var (available, version) = await updater.CheckForUpdateAsync();

        if (!available)
        {
            _vm.StatusText = $"You're on the latest version ({updater.CurrentVersion})";
            return;
        }

        _vm.StatusText = $"Downloading update v{version}...";
        var success = await updater.DownloadAndApplyAsync(progress =>
        {
            _vm.StatusText = $"Downloading update: {progress}%";
        });

        if (!success)
        {
            _vm.StatusText = "Update failed";
        }
        // If success, app restarts — won't reach here
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        switch (e.Key)
        {
            case Key.R when !ctrl:
                if (!_rKeyHeld)
                {
                    _rKeyHeld = true;
                    _vm.ToggleRecordingCommand.Execute(null);
                }
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
                if (_vm.SelectionStart >= 0 && _vm.SelectionEnd >= 0)
                    _vm.DeleteSelectionCommand.Execute(null);
                else
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
            case Key.Return when _vm.IsTrimMode:
                _vm.ConfirmTrimCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape when _vm.IsTrimMode:
                _vm.CancelTrimCommand.Execute(null);
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
            case Key.T when !_vm.IsTrimMode:
                _vm.EnterTrimModeCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.A when !ctrl:
                _vm.AutoTrimCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.U:
                CheckForUpdate();
                e.Handled = true;
                break;
            case Key.Q:
                Close();
                e.Handled = true;
                break;
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.R)
        {
            if (_rKeyHeld && _vm.RecordingState == RecordingState.Recording && IsPushToTalk())
            {
                _vm.ToggleRecordingCommand.Execute(null);
            }
            _rKeyHeld = false;
            e.Handled = true;
        }
    }

    private bool IsPushToTalk() => AppConfig.Load().PushToTalk;

    private bool _syncingSelection;

    private void SegmentList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection) return;
        if (sender is ListBox lb && lb.SelectedIndex >= 0)
        {
            _vm.SetSelectedIndex(lb.SelectedIndex);
        }
    }

    private void SyncListBoxSelection()
    {
        var segmentList = this.FindControl<ListBox>("SegmentList");
        if (segmentList == null) return;

        _syncingSelection = true;
        try
        {
            if (_vm.SelectedSegment != null)
                segmentList.SelectedItem = _vm.SelectedSegment;
            else
                segmentList.SelectedIndex = -1;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private static AppUpdater CreateUpdater()
    {
        var updater = new AppUpdater();
        updater.SetAppAssembly(typeof(MainWindow).Assembly);
        return updater;
    }

    private void Version_Click(object? sender, RoutedEventArgs e)
    {
        CheckForUpdate();
    }

    private void AutoTrimPreview_Enter(object? sender, PointerEventArgs e)
    {
        _vm.ComputeAutoTrimPreview();
    }

    private void AutoTrimPreview_Exit(object? sender, PointerEventArgs e)
    {
        _vm.AutoTrimPreview = null;
    }

    private void GitHub_Click(object? sender, RoutedEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://github.com/0xMMA/recap",
            UseShellExecute = true
        });
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
