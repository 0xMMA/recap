using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NAudio.Wave;
using Recap.Core.Audio;
using Recap.Core.Config;
using Recap.Core.Api;
using Recap.Core.Logging;
using Recap.Core.Models;
using Recap.Core.State;

namespace Recap.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private SessionState _state;
    private readonly AudioEngine _audio;
    private readonly ScribeClient _scribe;
    private AppConfig _config;

    [ObservableProperty]
    private SegmentViewModel? _selectedSegment;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _activeLanguage = "de";

    [ObservableProperty]
    private RecordingState _recordingState = RecordingState.Idle;

    [ObservableProperty]
    private int _segmentCount;

    [ObservableProperty]
    private string _totalDuration = "0s";

    [ObservableProperty]
    private bool _hasApiKey;

    [ObservableProperty]
    private string? _transcriptText;

    [ObservableProperty]
    private bool _isTranscribing;

    [ObservableProperty]
    private float[]? _waveformPeaks;

    [ObservableProperty]
    private bool _isTrimMode;

    [ObservableProperty]
    private double _trimLeft;

    [ObservableProperty]
    private double _trimRight = 1.0;

    public ObservableCollection<SegmentViewModel> Segments { get; } = new();

    private string TempDir => Path.Combine(Path.GetTempPath(), "recap", _state.SessionId);

    public MainWindowViewModel()
    {
        _config = AppConfig.Load();
        _state = new SessionState();
        _audio = new AudioEngine();
        _scribe = new ScribeClient { ApiKey = _config.ApiKey };

        ActiveLanguage = _config.DefaultLanguage;
        HasApiKey = !string.IsNullOrEmpty(_config.ApiKey);
    }

    [RelayCommand]
    private void ToggleRecording()
    {
        try
        {
            if (RecordingState == RecordingState.Recording)
            {
                var segment = _audio.StopRecording();
                _state.AddSegment(segment);
                RecordingState = RecordingState.Idle;
                _state.RecordingState = RecordingState.Idle;
                SyncSegments();
                StatusText = $"Recorded segment {_state.Segments.Count}";
                Log.Info($"Recording stopped: segment {segment.Index}, duration {segment.Duration:mm\\:ss\\.f}");
            }
            else
            {
                Directory.CreateDirectory(TempDir);
                var fileName = $"{DateTime.Now:HHmmss-fff}.wav";
                var outputPath = Path.Combine(TempDir, fileName);
                _audio.StartRecording(outputPath);
                RecordingState = RecordingState.Recording;
                _state.RecordingState = RecordingState.Recording;
                StatusText = "Recording...";
                Log.Info("Recording started");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            Log.Error("Recording error", ex);
            RecordingState = RecordingState.Idle;
            _state.RecordingState = RecordingState.Idle;
        }
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (_state.Segments.Count == 0)
        {
            StatusText = "Nothing to delete";
            return;
        }

        _state.DeleteSelected();
        SyncSegments();
        StatusText = "Deleted segment(s)";
    }

    [RelayCommand]
    private void Undo()
    {
        if (_state.Undo())
        {
            SyncSegments();
            StatusText = "Undo successful";
        }
        else
        {
            StatusText = "Nothing to undo";
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        if (_state.Segments.Count == 0) return;
        _state.SelectAll();
        SyncSegments();
        StatusText = "Selected all segments";
    }

    [RelayCommand]
    private void PlaySelected()
    {
        var segment = _state.GetSelected();
        if (segment == null)
        {
            StatusText = "No segment selected";
            return;
        }

        if (!segment.HasFile)
        {
            StatusText = "Segment file missing";
            return;
        }

        try
        {
            _audio.Play(segment.FilePath);
            StatusText = $"Playing segment {segment.Index + 1}";
        }
        catch (Exception ex)
        {
            StatusText = $"Playback error: {ex.Message}";
            Log.Error("Playback error", ex);
        }
    }

    [RelayCommand]
    private void PlayAll()
    {
        var validFiles = _state.Segments
            .Where(s => s.HasFile)
            .Select(s => s.FilePath)
            .ToList();

        if (validFiles.Count == 0)
        {
            StatusText = "No segments to play";
            return;
        }

        try
        {
            var splicedPath = Path.Combine(TempDir, "_spliced_playback.wav");
            Directory.CreateDirectory(TempDir);
            AudioEngine.SpliceSegments(validFiles, splicedPath);
            _audio.Play(splicedPath);
            StatusText = $"Playing all ({validFiles.Count} segments)";
        }
        catch (Exception ex)
        {
            StatusText = $"Playback error: {ex.Message}";
            Log.Error("Play all error", ex);
        }
    }

    [RelayCommand]
    private void TogglePause()
    {
        _audio.TogglePause();
        StatusText = _audio.IsPaused ? "Paused" : (_audio.IsPlaying ? "Playing" : "Stopped");
    }

    [RelayCommand]
    private async Task TranscribeAsync()
    {
        if (IsTranscribing)
        {
            StatusText = "Already transcribing";
            return;
        }

        if (!HasApiKey)
        {
            StatusText = "API key not configured";
            return;
        }

        var segments = _state.GetSelectedSegments();
        if (segments.Count == 0)
        {
            StatusText = "No segments to transcribe";
            return;
        }

        var validFiles = segments.Where(s => s.HasFile).Select(s => s.FilePath).ToList();
        if (validFiles.Count == 0)
        {
            StatusText = "No valid segment files";
            return;
        }

        IsTranscribing = true;
        StatusText = "Transcribing...";
        Log.Info($"Transcription started: {validFiles.Count} segment(s)");

        try
        {
            string filePath;
            if (validFiles.Count == 1)
            {
                filePath = validFiles[0];
            }
            else
            {
                filePath = Path.Combine(TempDir, "_spliced_transcribe.wav");
                Directory.CreateDirectory(TempDir);
                AudioEngine.SpliceSegments(validFiles, filePath);
            }

            var lang = ActiveLanguage == "auto" ? null : ActiveLanguage;
            var text = await _scribe.TranscribeAsync(filePath, lang, _config.AudioEvents);
            TranscriptText = text;
            _state.TranscriptText = text;
            StatusText = "Transcription complete";
            Log.Info("Transcription completed successfully");
        }
        catch (Exception ex)
        {
            StatusText = $"Transcription failed: {ex.Message}";
            Log.Error("Transcription failed", ex);
        }
        finally
        {
            IsTranscribing = false;
        }
    }

    [RelayCommand]
    private async Task TranscribeAllAsync()
    {
        if (IsTranscribing)
        {
            StatusText = "Already transcribing";
            return;
        }

        if (!HasApiKey)
        {
            StatusText = "API key not configured";
            return;
        }

        var validFiles = _state.Segments
            .Where(s => s.HasFile)
            .Select(s => s.FilePath)
            .ToList();

        if (validFiles.Count == 0)
        {
            StatusText = "No segments to transcribe";
            return;
        }

        IsTranscribing = true;
        StatusText = "Transcribing all...";
        Log.Info($"Transcribe all started: {validFiles.Count} segment(s)");

        try
        {
            var splicedPath = Path.Combine(TempDir, "_spliced_transcribe_all.wav");
            Directory.CreateDirectory(TempDir);
            AudioEngine.SpliceSegments(validFiles, splicedPath);

            var lang = ActiveLanguage == "auto" ? null : ActiveLanguage;
            var text = await _scribe.TranscribeAsync(splicedPath, lang, _config.AudioEvents);
            TranscriptText = text;
            _state.TranscriptText = text;
            StatusText = "Transcription complete";
            Log.Info("Transcribe all completed successfully");
        }
        catch (Exception ex)
        {
            StatusText = $"Transcription failed: {ex.Message}";
            Log.Error("Transcribe all failed", ex);
        }
        finally
        {
            IsTranscribing = false;
        }
    }

    [RelayCommand]
    private void CycleLanguage()
    {
        ActiveLanguage = ActiveLanguage switch
        {
            "de" => "en",
            "en" => "auto",
            "auto" => "de",
            _ => "de"
        };
        _state.ActiveLanguage = ActiveLanguage;
        StatusText = $"Language: {ActiveLanguage}";
    }

    [RelayCommand]
    private void NewSession()
    {
        if (_state.IsDirty && _state.Segments.Count > 0)
        {
            SessionPersistence.SaveSessionManifest(_state);
        }

        _audio.StopPlayback();
        _state = new SessionState();
        _state.ActiveLanguage = ActiveLanguage;
        SyncSegments();
        TranscriptText = null;
        StatusText = "New session";
        Log.Info("New session created");
    }

    [RelayCommand]
    private void OpenInExplorer()
    {
        try
        {
            var dir = TempDir;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
            StatusText = $"Opened: {dir}";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not open folder: {ex.Message}";
        }
    }

    public void MoveSelection(int delta)
    {
        _state.MoveSelection(delta);
        SyncSegments();
    }

    public void ExtendSelection(int delta)
    {
        _state.ExtendSelection(delta);
        SyncSegments();
    }

    public void SyncSegments()
    {
        Segments.Clear();
        foreach (var seg in _state.Segments)
            Segments.Add(new SegmentViewModel(seg));

        SegmentCount = _state.Segments.Count;
        TotalDuration = HumanizeDuration(_state.TotalDuration);

        if (_state.Segments.Count > 0 && _state.SelectedIndex < Segments.Count)
            SelectedSegment = Segments[_state.SelectedIndex];
        else
            SelectedSegment = null;
    }

    partial void OnSelectedSegmentChanged(SegmentViewModel? value)
    {
        if (value != null && value.HasFile)
        {
            WaveformPeaks = WaveformData.GetPeaks(value.FilePath, 500);
        }
        else
        {
            WaveformPeaks = null;
        }
    }

    public float[]? GetLiveWaveform(int width)
    {
        if (_state.RecordingState != RecordingState.Recording) return null;
        return _audio.GetWaveformSnapshot(width);
    }

    public void UpdateConfig(AppConfig config)
    {
        _config = config;
        _scribe.ApiKey = config.ApiKey;
        HasApiKey = !string.IsNullOrEmpty(config.ApiKey);
        ActiveLanguage = config.DefaultLanguage;
    }

    public void RecoverSession()
    {
        try
        {
            var recovered = SessionPersistence.TryRecover();
            if (recovered != null && recovered.Segments.Count > 0)
            {
                _state = recovered;
                _state.ActiveLanguage = ActiveLanguage;
                SyncSegments();
                StatusText = $"Recovered {_state.Segments.Count} segment(s)";
                Log.Info($"Session recovered: {_state.Segments.Count} segments");
            }
        }
        catch (Exception ex)
        {
            Log.Error("Session recovery failed", ex);
        }
    }

    [RelayCommand]
    public void EnterTrimMode()
    {
        var seg = _state.GetSelected();
        if (seg == null || !seg.HasFile) { StatusText = "No segment to trim"; return; }
        IsTrimMode = true;
        TrimLeft = 0.0;
        TrimRight = 1.0;
        StatusText = "Trim mode — drag markers or use Enter to confirm, Esc to cancel";
    }

    [RelayCommand]
    public void ConfirmTrim()
    {
        if (!IsTrimMode) return;
        var seg = _state.GetSelected();
        if (seg == null || !seg.HasFile) return;

        try
        {
            ApplyTrim(seg, TrimLeft, TrimRight);
            WaveformData.Invalidate(seg.FilePath);
            seg.RefreshHasFile();
            WaveformPeaks = WaveformData.GetPeaks(seg.FilePath, 500);
            SyncSegments();
            StatusText = $"Trimmed segment {seg.Index + 1}";
            Log.Info($"Trimmed segment {seg.Index + 1}: [{TrimLeft:P0}-{TrimRight:P0}]");
        }
        catch (Exception ex)
        {
            StatusText = $"Trim failed: {ex.Message}";
            Log.Error("Trim failed", ex);
        }
        IsTrimMode = false;
    }

    [RelayCommand]
    public void CancelTrim()
    {
        IsTrimMode = false;
        StatusText = "Trim cancelled";
    }

    private static void RetryFileOp(Action op)
    {
        for (int i = 0; i < 3; i++)
        {
            try { op(); return; }
            catch (IOException) when (i < 2) { Thread.Sleep(100); }
        }
    }

    private void ApplyTrim(Segment segment, double leftFraction, double rightFraction)
    {
        var tempPath = segment.FilePath + ".trim.wav";
        using (var stream = new FileStream(segment.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new WaveFileReader(stream))
        {
            var totalSamples = reader.SampleCount;
            long startSample = (long)(leftFraction * totalSamples);
            long endSample = (long)(rightFraction * totalSamples);

            using var writer = new WaveFileWriter(tempPath, reader.WaveFormat);
            reader.Position = startSample * reader.WaveFormat.BlockAlign;
            long bytesToRead = (endSample - startSample) * reader.WaveFormat.BlockAlign;
            var buffer = new byte[Math.Min(bytesToRead, 65536)];
            long remaining = bytesToRead;
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(buffer.Length, remaining);
                int read = reader.Read(buffer, 0, toRead);
                if (read == 0) break;
                writer.Write(buffer, 0, read);
                remaining -= read;
            }
        }

        WaveformData.Invalidate(segment.FilePath);
        RetryFileOp(() => File.Delete(segment.FilePath));
        File.Move(tempPath, segment.FilePath);

        using var trimmed = new WaveFileReader(segment.FilePath);
        segment.Duration = trimmed.TotalTime;
    }

    [RelayCommand]
    public void AutoTrim()
    {
        var segments = _state.GetSelectedSegments().Where(s => s.HasFile).ToList();
        if (segments.Count == 0) { StatusText = "No segments to auto-trim"; return; }

        int trimmed = 0;
        foreach (var seg in segments)
        {
            var outputPath = seg.FilePath + ".autotrim.wav";
            var result = AutoTrimmer.Trim(seg.FilePath, outputPath);
            if (result.Success && result.OutputPath != null)
            {
                try
                {
                    WaveformData.Invalidate(seg.FilePath);
                    RetryFileOp(() => File.Delete(seg.FilePath));
                    File.Move(result.OutputPath, seg.FilePath);
                    using var reader = new WaveFileReader(seg.FilePath);
                    seg.Duration = reader.TotalTime;
                    seg.RefreshHasFile();
                    trimmed++;
                }
                catch (IOException ex)
                {
                    StatusText = $"Auto-trim file error: {ex.Message}";
                    Log.Error("Auto-trim file replace failed", ex);
                }
            }
            else
            {
                StatusText = result.Error ?? "Auto-trim failed";
            }
        }
        if (trimmed > 0)
        {
            SyncSegments();
            var sel = _state.GetSelected();
            if (sel != null && sel.HasFile)
                WaveformPeaks = WaveformData.GetPeaks(sel.FilePath, 500);
            StatusText = $"Auto-trimmed {trimmed} segment(s)";
        }
    }

    private static string HumanizeDuration(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1)
            return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
        if (ts.TotalSeconds >= 1)
            return $"~{(int)ts.TotalSeconds}s";
        return "0s";
    }

    public void Cleanup()
    {
        try
        {
            if (_state.IsDirty && _state.Segments.Count > 0)
                SessionPersistence.SaveSessionManifest(_state);

            _audio.Dispose();
            SessionPersistence.PruneOldSessions();
        }
        catch (Exception ex)
        {
            Log.Error("Cleanup error", ex);
        }
    }
}
