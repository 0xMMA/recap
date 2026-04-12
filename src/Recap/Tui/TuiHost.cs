using Recap.Audio;
using Recap.Config;
using Recap.Interop;
using Recap.Models;
using Recap.State;
using Recap.Api;
using Recap.Updates;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Recap.Tui;

public class TuiHost
{
    private readonly SessionState _state = new();
    private readonly AudioEngine _audio = new();
    private readonly ScribeClient _scribe = new();
    private readonly AppUpdater _updater = new();
    private AppConfig _config;
    private bool _running = true;
    private bool _pendingSave;
    private bool _pendingSettings;
    private bool _pendingTrim;
    private bool _pendingAutoTrim;
    private bool _pendingResultActions;
    private bool _pendingNewSession;
    private bool _pendingUpdate;

    public TuiHost()
    {
        _config = AppConfig.Load();
        _scribe.ApiKey = _config.ApiKey;
        _state.ActiveLanguage = _config.DefaultLanguage;
    }

    public string TempDir => Path.Combine(Path.GetTempPath(), "recap", _state.SessionId);

    public async Task RunAsync()
    {
        // First-run wizard
        SettingsDialog.RunFirstRunWizard(_config);
        _scribe.ApiKey = _config.ApiKey;
        _state.ActiveLanguage = _config.DefaultLanguage;

        // Try crash recovery
        var recovered = SessionPersistence.TryRecover();
        if (recovered != null && recovered.Segments.Count > 0)
        {
            AnsiConsole.Clear();
            if (AnsiConsole.Confirm($"[yellow]Found {recovered.Segments.Count} segment(s) from previous session. Recover?[/]"))
            {
                foreach (var seg in recovered.Segments)
                    _state.AddSegment(seg);
                _state.LastActionResult = $"Recovered {recovered.Segments.Count} segment(s)";
            }
        }

        // Prune old sessions
        SessionPersistence.PruneOldSessions();

        // Background update check on startup
        _ = CheckForUpdateOnStartupAsync();

        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("Recap").Color(Color.Cyan1));

        while (_running)
        {
            await AnsiConsole.Live(BuildLayout())
                .AutoClear(false)
                .Overflow(VerticalOverflow.Ellipsis)
                .StartAsync(async ctx =>
                {
                    while (_running && !NeedBreakFromLive())
                    {
                        ctx.UpdateTarget(BuildLayout());

                        // Push-to-talk: stop recording when R released
                        if (_config.PushToTalk && _state.RecordingState == RecordingState.Recording
                            && !KeyState.IsRKeyDown())
                        {
                            StopRecording();
                        }

                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(true);
                            await HandleKeyAsync(key);
                        }

                        await Task.Delay(66); // ~15fps
                    }
                });

            // Handle modal dialogs outside Live context
            await HandleModalsAsync();
        }

        // Save manifest on exit
        if (_state.Segments.Count > 0)
            SessionPersistence.SaveSessionManifest(_state);

        _audio.Dispose();
    }

    private bool NeedBreakFromLive() =>
        _pendingSave || _pendingSettings || _pendingTrim || _pendingAutoTrim
        || _pendingResultActions || _pendingNewSession || _pendingUpdate;

    private async Task HandleModalsAsync()
    {
        if (_pendingSave)
        {
            _pendingSave = false;
            AnsiConsole.Clear();
            var result = SaveDialog.Show(_state);
            if (result != null)
            {
                _state.LastActionResult = $"Saved to {result}";
                _state.IsDirty = false;
            }
            else
            {
                _state.LastActionResult = "Save cancelled";
            }
            AnsiConsole.Clear();
        }

        if (_pendingSettings)
        {
            _pendingSettings = false;
            SettingsDialog.Show(_config);
            _scribe.ApiKey = _config.ApiKey;
            _state.ActiveLanguage = _config.DefaultLanguage;
            _state.LastActionResult = "Settings updated";
            AnsiConsole.Clear();
        }

        if (_pendingTrim)
        {
            _pendingTrim = false;
            var seg = _state.GetSelected();
            if (seg != null)
            {
                var trimMode = new TrimMode(_state);
                if (trimMode.Run(seg))
                    _state.LastActionResult = $"Trimmed segment {seg.Index + 1}";
                else
                    _state.LastActionResult = "Trim cancelled";
            }
            AnsiConsole.Clear();
        }

        if (_pendingAutoTrim)
        {
            _pendingAutoTrim = false;
            var segments = _state.GetSelectedSegments();
            int trimmed = 0;
            foreach (var seg in segments)
            {
                var outputPath = seg.FilePath + ".autotrim.wav";
                var result = AutoTrimmer.Trim(seg.FilePath, outputPath);
                if (result.Success && result.OutputPath != null)
                {
                    File.Delete(seg.FilePath);
                    File.Move(result.OutputPath, seg.FilePath);
                    using var reader = new NAudio.Wave.WaveFileReader(seg.FilePath);
                    seg.Duration = reader.TotalTime;
                    trimmed++;
                }
                else
                {
                    _state.LastActionResult = result.Error ?? "Auto-trim failed";
                }
            }
            if (trimmed > 0)
                _state.LastActionResult = $"Auto-trimmed {trimmed} segment(s)";
        }

        if (_pendingResultActions)
        {
            _pendingResultActions = false;
            ResultPanel.ShowActionMenu(_state);
            AnsiConsole.Clear();
        }

        if (_pendingUpdate)
        {
            _pendingUpdate = false;
            await HandleUpdateAsync();
            AnsiConsole.Clear();
        }

        if (_pendingNewSession)
        {
            _pendingNewSession = false;
            AnsiConsole.Clear();
            if (_state.IsDirty)
            {
                if (AnsiConsole.Confirm("[yellow]Unsaved segments exist. Start new session anyway?[/]"))
                {
                    SessionPersistence.SaveSessionManifest(_state);
                    _state.Clear();
                    _state.LastActionResult = "New session started";
                }
                else
                {
                    _state.LastActionResult = "New session cancelled";
                }
            }
            else
            {
                _state.Clear();
                _state.LastActionResult = "New session started";
            }
            AnsiConsole.Clear();
        }
    }

    private IRenderable BuildLayout()
    {
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Main")
                    .SplitColumns(
                        new Layout("Segments").Size(45),
                        new Layout("Content")
                    ),
                new Layout("Waveform").Size(3),
                new Layout("StatusBar").Size(1)
            );

        layout["Segments"].Update(
            new Panel(new SegmentListPanel(_state))
                .Header("[bold]Segments[/]")
                .Border(BoxBorder.Rounded)
                .Expand()
        );

        var contentRenderable = GetContentRenderable();
        layout["Content"].Update(
            new Panel(contentRenderable)
                .Header("[bold]Recap[/]")
                .Border(BoxBorder.Rounded)
                .Expand()
        );

        if (_state.RecordingState == RecordingState.Recording)
        {
            var width = AnsiConsole.Profile.Width - 4;
            var peaks = _audio.GetWaveformSnapshot(Math.Max(1, width));
            var waveText = WaveformRenderer.Render(peaks, width);
            layout["Waveform"].Update(
                new Panel(new Markup($"[red]{Markup.Escape(waveText)}[/]"))
                    .Border(BoxBorder.None)
            );
        }
        else
        {
            layout["Waveform"].Update(new Panel(new Markup("[grey]---[/]")).Border(BoxBorder.None));
        }

        layout["StatusBar"].Update(
            new StatusBar(_state, () => !string.IsNullOrEmpty(_config.ApiKey))
        );

        return layout;
    }

    private IRenderable GetContentRenderable()
    {
        if (_state.Mode == AppMode.ResultView && _state.TranscriptText != null)
        {
            return new Markup(
                $"[bold]Transcript:[/]\n\n{Markup.Escape(_state.TranscriptText)}\n\n" +
                "[grey]C[/] Copy │ [grey]Enter[/] Actions │ [grey]Esc[/] Back"
            );
        }

        return new Markup(
            "[bold]Keybinds:[/]\n\n" +
            "[grey]R[/] Record/Stop  │  [grey]↑/↓[/] Navigate  │  [grey]Del[/] Delete\n" +
            "[grey]Ctrl+Z[/] Undo  │  [grey]Ctrl+A[/] Select all  │  [grey]Shift+↑/↓[/] Multi-select\n" +
            "[grey]Enter[/] Play segment  │  [grey]Shift+Enter[/] Play all\n" +
            "[grey]Space[/] Pause  │  [grey]S[/] Save  │  [grey]X[/] Transcribe  │  [grey]Shift+X[/] Transcribe all\n" +
            "[grey]T[/] Trim  │  [grey]A[/] Auto-trim  │  [grey]L[/] Language\n" +
            "[grey]N[/] New session  │  [grey]U[/] Update  │  [grey]Ctrl+,[/] Settings  │  [grey]Q[/] Quit"
        );
    }

    private async Task HandleKeyAsync(ConsoleKeyInfo key)
    {
        switch (_state.Mode)
        {
            case AppMode.Normal:
            case AppMode.Recording:
            case AppMode.Playing:
                await HandleMainKeyAsync(key);
                break;
            case AppMode.ResultView:
                await HandleResultKeyAsync(key);
                break;
        }
    }

    private async Task HandleMainKeyAsync(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.R when !key.Modifiers.HasFlag(ConsoleModifiers.Control):
                ToggleRecording();
                break;

            case ConsoleKey.UpArrow when key.Modifiers.HasFlag(ConsoleModifiers.Shift):
                _state.ExtendSelection(-1);
                break;

            case ConsoleKey.DownArrow when key.Modifiers.HasFlag(ConsoleModifiers.Shift):
                _state.ExtendSelection(1);
                break;

            case ConsoleKey.UpArrow:
                _state.MoveSelection(-1);
                break;

            case ConsoleKey.DownArrow:
                _state.MoveSelection(1);
                break;

            case ConsoleKey.Delete:
            case ConsoleKey.Backspace:
                _state.DeleteSelected();
                _state.LastActionResult = "Segment deleted";
                break;

            case ConsoleKey.Z when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                _state.LastActionResult = _state.Undo() ? "Undo successful" : "Nothing to undo";
                break;

            case ConsoleKey.Enter when key.Modifiers.HasFlag(ConsoleModifiers.Shift):
                PlaySpliced();
                break;

            case ConsoleKey.Enter when _state.Mode == AppMode.ResultView:
                _pendingResultActions = true;
                break;

            case ConsoleKey.Enter:
                PlaySelected();
                break;

            case ConsoleKey.Spacebar when _audio.IsPlaying || _audio.IsPaused:
                _audio.TogglePause();
                break;

            case ConsoleKey.S:
                _pendingSave = true;
                break;

            case ConsoleKey.X when key.Modifiers.HasFlag(ConsoleModifiers.Shift):
                await TranscribeAllAsync();
                break;

            case ConsoleKey.X:
                await TranscribeAsync();
                break;

            case ConsoleKey.A when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                _state.SelectAll();
                _state.LastActionResult = $"Selected all {_state.Segments.Count} segments";
                break;

            case ConsoleKey.T:
                _pendingTrim = true;
                break;

            case ConsoleKey.A:
                _pendingAutoTrim = true;
                break;

            case ConsoleKey.L:
                CycleLanguage();
                break;

            case ConsoleKey.N:
                _pendingNewSession = true;
                break;

            case ConsoleKey.Q:
                _running = false;
                break;

            case ConsoleKey.U:
                _pendingUpdate = true;
                break;

            // Ctrl+, for settings (comma key)
            case ConsoleKey.OemComma when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                _pendingSettings = true;
                break;
        }
    }

    private async Task HandleResultKeyAsync(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.C:
                if (_state.TranscriptText != null)
                {
                    await TextCopy.ClipboardService.SetTextAsync(_state.TranscriptText);
                    _state.LastActionResult = "Copied to clipboard";
                }
                break;

            case ConsoleKey.Enter:
                _pendingResultActions = true;
                break;

            case ConsoleKey.Escape:
                _state.Mode = AppMode.Normal;
                _state.TranscriptText = null;
                break;
        }
    }

    private void ToggleRecording()
    {
        if (_state.RecordingState == RecordingState.Recording)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
    }

    private void StartRecording()
    {
        // Use timestamp-based name to avoid collisions after segment deletes
        var name = DateTime.Now.ToString("HHmmss-fff");
        var path = Path.Combine(TempDir, $"{name}.wav");

        try
        {
            _audio.StartRecording(path);
            _state.RecordingState = RecordingState.Recording;
            _state.Mode = AppMode.Recording;
            _state.LastActionResult = "Recording...";
        }
        catch (IOException ex)
        {
            _state.LastActionResult = $"Cannot start recording: {ex.Message}";
        }
    }

    private void StopRecording()
    {
        var segment = _audio.StopRecording();
        _state.AddSegment(segment);
        _state.RecordingState = RecordingState.Idle;
        _state.Mode = AppMode.Normal;
        _state.LastActionResult = $"Recorded {segment.Duration:mm\\:ss\\.f}";
        SessionPersistence.SaveSessionManifest(_state);
    }

    private void PlaySelected()
    {
        var seg = _state.GetSelected();
        if (seg == null) return;
        _audio.Play(seg.FilePath);
        _state.Mode = AppMode.Playing;
        _state.LastActionResult = $"Playing segment {seg.Index + 1}";
    }

    private void PlaySpliced()
    {
        if (_state.Segments.Count == 0) return;
        var splicedPath = Path.Combine(TempDir, "_spliced.wav");
        AudioEngine.SpliceSegments(_state.Segments.Select(s => s.FilePath), splicedPath);
        _audio.Play(splicedPath);
        _state.Mode = AppMode.Playing;
        _state.LastActionResult = "Playing all segments";
    }

    private async Task TranscribeAsync()
    {
        if (string.IsNullOrEmpty(_config.ApiKey))
        {
            _state.LastActionResult = "No API key. Press Ctrl+, to configure.";
            return;
        }

        var segments = _state.GetSelectedSegments();
        if (segments.Count == 0 && _state.Segments.Count > 0)
        {
            var splicedPath = Path.Combine(TempDir, "_transcribe.wav");
            AudioEngine.SpliceSegments(_state.Segments.Select(s => s.FilePath), splicedPath);
            segments = new() { new Models.Segment { FilePath = splicedPath } };
        }

        if (segments.Count == 0)
        {
            _state.LastActionResult = "No segments to transcribe";
            return;
        }

        _state.LastActionResult = "Transcribing...";

        try
        {
            var lang = _state.ActiveLanguage == "auto" ? null : _state.ActiveLanguage;
            var results = new List<string>();

            foreach (var seg in segments)
            {
                var text = await _scribe.TranscribeAsync(seg.FilePath, lang, _config.AudioEvents);
                results.Add(text);
            }

            _state.TranscriptText = string.Join("\n\n---\n\n", results);
            _state.Mode = AppMode.ResultView;
            _state.LastActionResult = "Transcription complete";
        }
        catch (Exception ex)
        {
            _state.LastActionResult = $"Transcribe failed: {ex.Message}";
        }
    }

    private async Task TranscribeAllAsync()
    {
        if (string.IsNullOrEmpty(_config.ApiKey))
        {
            _state.LastActionResult = "No API key. Press Ctrl+, to configure.";
            return;
        }

        if (_state.Segments.Count == 0)
        {
            _state.LastActionResult = "No segments to transcribe";
            return;
        }

        _state.LastActionResult = "Transcribing all segments...";

        try
        {
            var lang = _state.ActiveLanguage == "auto" ? null : _state.ActiveLanguage;
            var splicedPath = Path.Combine(TempDir, "_transcribe_all.wav");
            AudioEngine.SpliceSegments(_state.Segments.Select(s => s.FilePath), splicedPath);
            var text = await _scribe.TranscribeAsync(splicedPath, lang, _config.AudioEvents);

            _state.TranscriptText = text;
            _state.Mode = AppMode.ResultView;
            _state.LastActionResult = $"Transcribed all {_state.Segments.Count} segments";
        }
        catch (Exception ex)
        {
            _state.LastActionResult = $"Transcribe failed: {ex.Message}";
        }
    }

    private void CycleLanguage()
    {
        _state.ActiveLanguage = _state.ActiveLanguage switch
        {
            "auto" => _config.DefaultLanguage,
            var l when l == _config.DefaultLanguage => "auto",
            _ => "auto"
        };
        _state.LastActionResult = $"Language: {_state.ActiveLanguage}";
    }

    private async Task CheckForUpdateOnStartupAsync()
    {
        var (available, version) = await _updater.CheckForUpdateAsync();
        if (available)
            _state.LastActionResult = $"Update available: v{version} — press U to update";
    }

    private async Task HandleUpdateAsync()
    {
        AnsiConsole.Clear();
        var (available, version) = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Checking for updates...", async _ => await _updater.CheckForUpdateAsync());

        if (!available)
        {
            AnsiConsole.MarkupLine($"[green]You are on the latest version ({_updater.CurrentVersion ?? "dev"}).[/]");
            AnsiConsole.MarkupLine("[grey]Press any key to continue.[/]");
            Console.ReadKey(true);
            return;
        }

        if (!AnsiConsole.Confirm($"[cyan]Update to v{version}?[/]"))
        {
            _state.LastActionResult = "Update skipped";
            return;
        }

        var success = await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("Downloading update...", maxValue: 100);
                return await _updater.DownloadAndApplyAsync(progress => task.Value = progress);
            });

        if (!success)
        {
            AnsiConsole.MarkupLine("[red]Update failed. Press any key to continue.[/]");
            Console.ReadKey(true);
            _state.LastActionResult = "Update failed";
        }
        // If success, app will have restarted — won't reach here
    }
}
