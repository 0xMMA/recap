# Avalonia 11 Transformation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transform Recap from Spectre.Console TUI to Avalonia 11 desktop app while preserving all 28 requirements and existing core logic.

**Architecture:** Extract non-UI code into `Recap.Core` class library. New `Recap.Desktop` project with Avalonia 11, MVVM via CommunityToolkit.Mvvm. Custom `WaveformControl` using `DrawingContext` for pixel-level audio visualization. All keyboard shortcuts preserved.

**Tech Stack:** .NET 10, Avalonia 11.3.x, CommunityToolkit.Mvvm, NAudio 2.3, Velopack, xUnit + Shouldly

---

## File Structure

```
src/
  Recap.Core/
    Recap.Core.csproj                    # net10.0 class library
    Audio/
      IAudioEngine.cs                    # NEW: cross-platform interface
      AudioEngine.cs                     # MOVE from src/Recap/Audio/
      AutoTrimmer.cs                     # MOVE from src/Recap/Audio/
    Api/ScribeClient.cs                  # MOVE from src/Recap/Api/
    Config/AppConfig.cs                  # MOVE from src/Recap/Config/
    Logging/Log.cs                       # MOVE from src/Recap/Logging/
    Models/
      Segment.cs                         # MOVE from src/Recap/Models/
      AppMode.cs                         # MOVE from src/Recap/Models/
      RecordingState.cs                  # MOVE from src/Recap/Models/
    State/
      SessionState.cs                    # MOVE from src/Recap/State/
      SessionPersistence.cs              # MOVE from src/Recap/State/
    Updates/AppUpdater.cs                # MOVE from src/Recap/Updates/

  Recap.Desktop/
    Recap.Desktop.csproj                 # net10.0, Avalonia 11, win-x64
    Program.cs                           # Avalonia entry + Velopack bootstrap
    App.axaml                            # Fluent theme
    App.axaml.cs                         # MainWindow startup + crash recovery
    ViewModels/
      MainWindowViewModel.cs             # Core VM: segments, recording, commands
      SegmentViewModel.cs                # Per-segment display wrapper
      SettingsViewModel.cs               # Settings dialog VM
    Views/
      MainWindow.axaml                   # Main layout: toolbar, segments, waveform, status
      MainWindow.axaml.cs                # Code-behind: keyboard routing, dialog hosting
      SettingsWindow.axaml               # Settings modal dialog
      SettingsWindow.axaml.cs
      TranscriptWindow.axaml             # Transcript result + actions
      TranscriptWindow.axaml.cs
    Controls/
      WaveformControl.cs                 # Custom control: DrawingContext waveform
    Converters/
      DurationConverter.cs               # TimeSpan → "mm:ss.f"
      BoolToColorConverter.cs            # Recording state → red/green

tests/
  Recap.Tests/
    Recap.Tests.csproj                   # MODIFY: ref Recap.Core instead of Recap
    SessionStateTests.cs                 # KEEP (update namespace)
    AudioEngineTests.cs                  # KEEP (update namespace)
    AutoTrimmerTests.cs                  # KEEP (update namespace)
    ViewModelTests.cs                    # NEW: MainWindowViewModel tests
    (TuiRenderTests.cs)                  # DELETE (Spectre-specific)
```

---

### Task 1: Extract Recap.Core

**Files:**
- Create: `src/Recap.Core/Recap.Core.csproj`
- Create: `src/Recap.Core/Audio/IAudioEngine.cs`
- Move: all non-UI .cs files from `src/Recap/` to `src/Recap.Core/`
- Modify: `tests/Recap.Tests/Recap.Tests.csproj` — reference Recap.Core
- Modify: `Recap.slnx` — add Recap.Core, remove old Recap
- Delete: `src/Recap/` (entire old project)

- [ ] **Step 1: Create Recap.Core project**

```bash
dotnet new classlib -n Recap.Core -o src/Recap.Core --framework net10.0
dotnet sln add src/Recap.Core/Recap.Core.csproj
```

- [ ] **Step 2: Add NuGet packages to Recap.Core**

```bash
dotnet add src/Recap.Core package NAudio --version 2.3.0
dotnet add src/Recap.Core package Velopack --version 0.0.1298
dotnet add src/Recap.Core package TextCopy --version 6.2.1
```

- [ ] **Step 3: Create IAudioEngine interface**

```csharp
// src/Recap.Core/Audio/IAudioEngine.cs
using Recap.Core.Models;

namespace Recap.Core.Audio;

public interface IAudioEngine : IDisposable
{
    bool IsRecording { get; }
    bool IsPlaying { get; }
    bool IsPaused { get; }

    string StartRecording(string outputPath);
    Segment StopRecording();
    float[] GetWaveformSnapshot(int columns);

    void Play(string filePath);
    void TogglePause();
    void StopPlayback();

    static abstract void SpliceSegments(IEnumerable<string> files, string outputPath);
}
```

- [ ] **Step 4: Move all source files to Recap.Core**

Move these directories preserving structure:
- `src/Recap/Audio/` → `src/Recap.Core/Audio/`
- `src/Recap/Api/` → `src/Recap.Core/Api/`
- `src/Recap/Config/` → `src/Recap.Core/Config/`
- `src/Recap/Logging/` → `src/Recap.Core/Logging/`
- `src/Recap/Models/` → `src/Recap.Core/Models/`
- `src/Recap/State/` → `src/Recap.Core/State/`
- `src/Recap/Updates/` → `src/Recap.Core/Updates/`
- `src/Recap/Interop/` → `src/Recap.Core/Interop/`

Update all namespaces: `Recap.Audio` → `Recap.Core.Audio`, etc.
Make `AudioEngine` implement `IAudioEngine`.
Remove `SpliceSegments` from the interface (keep as static method on AudioEngine).

- [ ] **Step 5: Update test project references**

```bash
dotnet remove tests/Recap.Tests reference src/Recap/Recap.csproj
dotnet add tests/Recap.Tests reference src/Recap.Core/Recap.Core.csproj
```

Update all `using` statements in test files: `Recap.Audio` → `Recap.Core.Audio`, etc.
Delete `TuiRenderTests.cs` (Spectre-specific).
Remove Spectre.Console.Testing package from test project.

- [ ] **Step 6: Remove old project, delete TUI code**

```bash
dotnet sln remove src/Recap/Recap.csproj
rm -rf src/Recap
```

- [ ] **Step 7: Verify build and tests**

```bash
dotnet build
dotnet test
```

Expected: build succeeds, all remaining tests pass (SessionState, AudioEngine, AutoTrimmer tests — ~34 tests after removing TuiRenderTests).

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "Extract Recap.Core from TUI project

Move all non-UI code (audio, API, config, models, state, logging,
updates) into Recap.Core class library. Add IAudioEngine interface
for future cross-platform support. Delete TUI code (Spectre.Console).
Update test project references."
```

---

### Task 2: Scaffold Recap.Desktop

**Files:**
- Create: `src/Recap.Desktop/Recap.Desktop.csproj`
- Create: `src/Recap.Desktop/Program.cs`
- Create: `src/Recap.Desktop/App.axaml`
- Create: `src/Recap.Desktop/App.axaml.cs`
- Create: `src/Recap.Desktop/Views/MainWindow.axaml`
- Create: `src/Recap.Desktop/Views/MainWindow.axaml.cs`
- Modify: `Recap.slnx` — add Recap.Desktop

- [ ] **Step 1: Create Avalonia project manually**

Create `src/Recap.Desktop/Recap.Desktop.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <Version>1.0.0</Version>
    <AssemblyName>Recap</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.3.13" />
    <PackageReference Include="Avalonia.Desktop" Version="11.3.13" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.3.13" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Recap.Core\Recap.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create Program.cs with Velopack bootstrap**

```csharp
using Avalonia;
using Velopack;

namespace Recap.Desktop;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
```

- [ ] **Step 3: Create App.axaml and App.axaml.cs**

App.axaml:
```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Recap.Desktop.App"
             RequestedThemeVariant="Dark">
    <Application.Styles>
        <FluentTheme />
    </Application.Styles>
</Application>
```

App.axaml.cs:
```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Recap.Desktop.Views;

namespace Recap.Desktop;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
```

- [ ] **Step 4: Create empty MainWindow**

MainWindow.axaml:
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="Recap.Desktop.Views.MainWindow"
        Title="Recap" Width="900" Height="600"
        MinWidth="640" MinHeight="400">
    <TextBlock Text="Recap" FontSize="24"
               HorizontalAlignment="Center"
               VerticalAlignment="Center" />
</Window>
```

MainWindow.axaml.cs:
```csharp
using Avalonia.Controls;

namespace Recap.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 5: Add to solution and verify**

```bash
dotnet sln add src/Recap.Desktop/Recap.Desktop.csproj
dotnet build
dotnet run --project src/Recap.Desktop
```

Expected: empty dark window with "Recap" text appears.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Scaffold Recap.Desktop with Avalonia 11

Empty Avalonia desktop app with Fluent dark theme, Velopack
bootstrap, and reference to Recap.Core."
```

---

### Task 3: MainWindowViewModel + Segment List

**Files:**
- Create: `src/Recap.Desktop/ViewModels/MainWindowViewModel.cs`
- Create: `src/Recap.Desktop/ViewModels/SegmentViewModel.cs`
- Create: `src/Recap.Desktop/Converters/DurationConverter.cs`
- Modify: `src/Recap.Desktop/Views/MainWindow.axaml` — add segment list + toolbar
- Modify: `src/Recap.Desktop/Views/MainWindow.axaml.cs` — wire VM
- Create: `tests/Recap.Tests/ViewModelTests.cs`

- [ ] **Step 1: Create SegmentViewModel**

```csharp
// src/Recap.Desktop/ViewModels/SegmentViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using Recap.Core.Models;

namespace Recap.Desktop.ViewModels;

public partial class SegmentViewModel : ObservableObject
{
    private readonly Segment _segment;

    public SegmentViewModel(Segment segment) => _segment = segment;

    public Segment Model => _segment;
    public int DisplayIndex => _segment.Index + 1;
    public string Duration => _segment.Duration.ToString(@"mm\:ss\.f");
    public string FilePath => _segment.FilePath;
    public bool HasFile => _segment.HasFile;

    public void Refresh() => OnPropertyChanged(string.Empty);
}
```

- [ ] **Step 2: Create MainWindowViewModel**

```csharp
// src/Recap.Desktop/ViewModels/MainWindowViewModel.cs
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recap.Core.Audio;
using Recap.Core.Config;
using Recap.Core.Api;
using Recap.Core.Logging;
using Recap.Core.Models;
using Recap.Core.State;

namespace Recap.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly SessionState _state = new();
    private readonly AudioEngine _audio = new();
    private readonly ScribeClient _scribe = new();
    private AppConfig _config;

    public ObservableCollection<SegmentViewModel> Segments { get; } = new();

    [ObservableProperty] private SegmentViewModel? _selectedSegment;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private string _activeLanguage = "de";
    [ObservableProperty] private RecordingState _recordingState = RecordingState.Idle;
    [ObservableProperty] private int _segmentCount;
    [ObservableProperty] private string _totalDuration = "00:00:00";
    [ObservableProperty] private bool _hasApiKey;
    [ObservableProperty] private string? _transcriptText;

    public SessionState State => _state;

    public string TempDir => Path.Combine(Path.GetTempPath(), "recap", _state.SessionId);

    public MainWindowViewModel()
    {
        _config = AppConfig.Load();
        _scribe.ApiKey = _config.ApiKey;
        _activeLanguage = _config.DefaultLanguage;
        _hasApiKey = !string.IsNullOrEmpty(_config.ApiKey);
        Log.Info($"Recap started — session {_state.SessionId}");
    }

    [RelayCommand]
    public void ToggleRecording()
    {
        if (_state.RecordingState == RecordingState.Recording)
            StopRecording();
        else
            StartRecording();
    }

    private void StartRecording()
    {
        var name = DateTime.Now.ToString("HHmmss-fff");
        var path = Path.Combine(TempDir, $"{name}.wav");
        try
        {
            _audio.StartRecording(path);
            RecordingState = RecordingState.Recording;
            StatusText = "Recording...";
            Log.Info($"Recording started: {path}");
        }
        catch (Exception ex)
        {
            StatusText = $"Cannot start recording: {ex.Message}";
            Log.Error("Recording start failed", ex);
        }
    }

    private void StopRecording()
    {
        var segment = _audio.StopRecording();
        _state.AddSegment(segment);
        SyncSegments();
        RecordingState = RecordingState.Idle;
        StatusText = $"Recorded {segment.Duration:mm\\:ss\\.f}";
        Log.Info($"Recording stopped: {segment.FilePath} ({segment.Duration:mm\\:ss\\.f})");
        SessionPersistence.SaveSessionManifest(_state);
    }

    [RelayCommand]
    public void DeleteSelected()
    {
        _state.DeleteSelected();
        SyncSegments();
        StatusText = "Segment deleted";
    }

    [RelayCommand]
    public void Undo()
    {
        StatusText = _state.Undo() ? "Undo successful" : "Nothing to undo";
        SyncSegments();
    }

    [RelayCommand]
    public void SelectAll()
    {
        _state.SelectAll();
        StatusText = $"Selected all {_state.Segments.Count} segments";
    }

    [RelayCommand]
    public void PlaySelected()
    {
        var seg = _state.GetSelected();
        if (seg == null) return;
        if (!seg.HasFile) { StatusText = "File missing"; return; }
        _audio.Play(seg.FilePath);
        StatusText = $"Playing segment {seg.Index + 1}";
    }

    [RelayCommand]
    public void PlayAll()
    {
        var valid = _state.Segments.Where(s => s.HasFile).ToList();
        if (valid.Count == 0) { StatusText = "No valid files"; return; }
        try
        {
            var path = Path.Combine(TempDir, "_spliced.wav");
            AudioEngine.SpliceSegments(valid.Select(s => s.FilePath), path);
            _audio.Play(path);
            StatusText = "Playing all";
        }
        catch (Exception ex) { StatusText = $"Playback failed: {ex.Message}"; }
    }

    [RelayCommand]
    public void TogglePause()
    {
        _audio.TogglePause();
    }

    [RelayCommand]
    public async Task TranscribeAsync()
    {
        if (!HasApiKey) { StatusText = "No API key — open Settings"; return; }
        var segments = _state.GetSelectedSegments().Where(s => s.HasFile).ToList();
        if (segments.Count == 0 && _state.Segments.Count > 0)
        {
            var valid = _state.Segments.Where(s => s.HasFile).ToList();
            if (valid.Count == 0) { StatusText = "No valid files"; return; }
            var path = Path.Combine(TempDir, "_transcribe.wav");
            AudioEngine.SpliceSegments(valid.Select(s => s.FilePath), path);
            segments = new() { new Segment { FilePath = path } };
        }
        if (segments.Count == 0) { StatusText = "Nothing to transcribe"; return; }

        StatusText = "Transcribing...";
        Log.Info($"Transcribing {segments.Count} segment(s)...");
        try
        {
            var lang = ActiveLanguage == "auto" ? null : ActiveLanguage;
            var results = new List<string>();
            foreach (var seg in segments)
            {
                var text = await _scribe.TranscribeAsync(seg.FilePath, lang, _config.AudioEvents);
                results.Add(text);
            }
            TranscriptText = string.Join("\n\n", results);
            StatusText = "Transcription complete";
            Log.Info($"Transcription complete — {TranscriptText.Length} chars");
        }
        catch (Exception ex)
        {
            StatusText = $"Transcribe failed: {ex.Message}";
            Log.Error("Transcription failed", ex);
        }
    }

    [RelayCommand]
    public async Task TranscribeAllAsync()
    {
        if (!HasApiKey) { StatusText = "No API key — open Settings"; return; }
        var valid = _state.Segments.Where(s => s.HasFile).ToList();
        if (valid.Count == 0) { StatusText = "No valid files"; return; }

        StatusText = "Transcribing all...";
        Log.Info($"Transcribing all — {valid.Count} segments");
        try
        {
            var lang = ActiveLanguage == "auto" ? null : ActiveLanguage;
            var path = Path.Combine(TempDir, "_transcribe_all.wav");
            AudioEngine.SpliceSegments(valid.Select(s => s.FilePath), path);
            TranscriptText = await _scribe.TranscribeAsync(path, lang, _config.AudioEvents);
            StatusText = $"Transcribed all {valid.Count} segments";
            Log.Info($"Transcribe-all complete — {TranscriptText.Length} chars");
        }
        catch (Exception ex)
        {
            StatusText = $"Transcribe failed: {ex.Message}";
            Log.Error("Transcribe-all failed", ex);
        }
    }

    [RelayCommand]
    public void CycleLanguage()
    {
        ActiveLanguage = ActiveLanguage switch
        {
            "auto" => _config.DefaultLanguage,
            var l when l == _config.DefaultLanguage => "auto",
            _ => "auto"
        };
        StatusText = $"Language: {ActiveLanguage}";
    }

    [RelayCommand]
    public void NewSession()
    {
        if (_state.IsDirty)
        {
            SessionPersistence.SaveSessionManifest(_state);
        }
        _state.Clear();
        SyncSegments();
        StatusText = "New session";
    }

    public void UpdateConfig(AppConfig config)
    {
        _config = config;
        _scribe.ApiKey = config.ApiKey;
        ActiveLanguage = config.DefaultLanguage;
        HasApiKey = !string.IsNullOrEmpty(config.ApiKey);
    }

    public void RecoverSession()
    {
        var recovered = SessionPersistence.TryRecover();
        if (recovered != null && recovered.Segments.Count > 0)
        {
            foreach (var seg in recovered.Segments)
                _state.AddSegment(seg);
            SyncSegments();
            StatusText = $"Recovered {recovered.Segments.Count} segment(s)";
        }
        SessionPersistence.PruneOldSessions();
    }

    public void MoveSelection(int delta)
    {
        _state.MoveSelection(delta);
        UpdateSelectedFromState();
    }

    public void ExtendSelection(int delta)
    {
        _state.ExtendSelection(delta);
    }

    private void SyncSegments()
    {
        Segments.Clear();
        foreach (var seg in _state.Segments)
            Segments.Add(new SegmentViewModel(seg));
        UpdateSelectedFromState();
        SegmentCount = _state.Segments.Count;
        TotalDuration = _state.TotalDuration.ToString(@"hh\:mm\:ss");
    }

    private void UpdateSelectedFromState()
    {
        var sel = _state.GetSelected();
        SelectedSegment = sel != null && _state.SelectedIndex < Segments.Count
            ? Segments[_state.SelectedIndex]
            : null;
    }

    public void Cleanup()
    {
        if (_state.Segments.Count > 0)
            SessionPersistence.SaveSessionManifest(_state);
        _audio.Dispose();
    }
}
```

- [ ] **Step 3: Create DurationConverter**

```csharp
// src/Recap.Desktop/Converters/DurationConverter.cs
using System.Globalization;
using Avalonia.Data.Converters;

namespace Recap.Desktop.Converters;

public class DurationConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimeSpan ts)
            return ts.ToString(@"mm\:ss\.f");
        return value?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

- [ ] **Step 4: Build MainWindow layout with segment list + toolbar + status bar**

MainWindow.axaml — full layout with toolbar buttons, segment ListBox, waveform placeholder, and status bar. All buttons bound to VM commands. Keyboard shortcuts via KeyBindings.

- [ ] **Step 5: Wire VM in MainWindow.axaml.cs**

Set DataContext to MainWindowViewModel. Call RecoverSession() on loaded. Call Cleanup() on closing.

- [ ] **Step 6: Write ViewModelTests**

```csharp
// tests/Recap.Tests/ViewModelTests.cs
// Test: ToggleRecording changes state
// Test: DeleteSelected removes segment
// Test: Undo restores segment
// Test: SelectAll selects all
// Test: CycleLanguage toggles
// Test: NewSession clears
```

- [ ] **Step 7: Build and run**

```bash
dotnet build
dotnet test
dotnet run --project src/Recap.Desktop
```

Expected: window shows toolbar, empty segment list, status bar.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "Add MainWindowViewModel, segment list, toolbar, status bar

MVVM architecture with CommunityToolkit.Mvvm. All recording,
playback, transcription, and session commands wired. Segment
list with selection. Keyboard shortcuts preserved."
```

---

### Task 4: WaveformControl

**Files:**
- Create: `src/Recap.Desktop/Controls/WaveformControl.cs`
- Modify: `src/Recap.Desktop/Views/MainWindow.axaml` — add waveform panel

- [ ] **Step 1: Create WaveformControl with DrawingContext rendering**

Custom Avalonia control that:
- Accepts `float[] Peaks` via styled property
- Draws filled waveform polygon using DrawingContext
- Green fill for waveform body, darker green for outline
- Handles resize via InvalidateVisual
- Shows flat line when no data

- [ ] **Step 2: Add to MainWindow center panel**

Bind `Peaks` property to selected segment's peak data. Load peaks from `WaveformRenderer.GetOrLoadPeaks()` (keep core peak loading, just draw differently).

Actually — move peak loading to Recap.Core's WaveformRenderer (keep the cache + normalization logic). WaveformControl just draws float[].

- [ ] **Step 3: Verify waveform renders for recorded segments**

- [ ] **Step 4: Commit**

---

### Task 5: Live Recording Waveform

**Files:**
- Modify: `src/Recap.Desktop/Controls/WaveformControl.cs` — or create `LiveWaveformControl.cs`
- Modify: `src/Recap.Desktop/ViewModels/MainWindowViewModel.cs` — timer for ring buffer polling

- [ ] **Step 1: Add DispatcherTimer to poll ring buffer during recording**

Every 66ms, call `_audio.GetWaveformSnapshot()` and update a `LivePeaks` property on the VM. Waveform control binds to this when recording.

- [ ] **Step 2: Switch waveform display between live (recording) and static (selected segment)**

- [ ] **Step 3: Commit**

---

### Task 6: Settings & Save Dialogs

**Files:**
- Create: `src/Recap.Desktop/Views/SettingsWindow.axaml`
- Create: `src/Recap.Desktop/Views/SettingsWindow.axaml.cs`
- Create: `src/Recap.Desktop/ViewModels/SettingsViewModel.cs`
- Modify: `src/Recap.Desktop/Views/MainWindow.axaml.cs` — open dialogs

- [ ] **Step 1: Create SettingsViewModel with config fields**

- [ ] **Step 2: Create SettingsWindow with form fields**

Fields: API key (password box), default language, push-to-talk toggle, audio events toggle, timestamp granularity dropdown. Save/Cancel buttons.

- [ ] **Step 3: Wire settings command in MainWindow**

Open as modal dialog. On save, call `vm.UpdateConfig()`.

- [ ] **Step 4: Implement save dialog**

Use Avalonia `StorageProvider.SaveFilePickerAsync()` for folder/file selection. Options: spliced single file vs individual segments.

- [ ] **Step 5: Commit**

---

### Task 7: Transcript Result Window

**Files:**
- Create: `src/Recap.Desktop/Views/TranscriptWindow.axaml`
- Create: `src/Recap.Desktop/Views/TranscriptWindow.axaml.cs`

- [ ] **Step 1: Create TranscriptWindow**

ScrollViewer with TextBox (read-only) for transcript text. Buttons: Copy, Save .md, Save .txt, Append, Close. Native scrolling — no manual offset needed.

- [ ] **Step 2: Wire transcript display**

When `TranscriptText` is set on VM, open TranscriptWindow. Copy button uses TextCopy. Save buttons use StorageProvider.

- [ ] **Step 3: Commit**

---

### Task 8: Trim Mode

**Files:**
- Modify: `src/Recap.Desktop/Controls/WaveformControl.cs` — add trim markers
- Modify: `src/Recap.Desktop/ViewModels/MainWindowViewModel.cs` — trim commands

- [ ] **Step 1: Add trim marker support to WaveformControl**

Two draggable vertical lines (left/right markers). Draw as colored vertical lines over waveform. Mouse drag to reposition. Selected region highlighted.

- [ ] **Step 2: Add trim commands to VM**

`EnterTrimMode`, `ConfirmTrim`, `CancelTrim`. Trim slices PCM between markers, replaces segment file.

- [ ] **Step 3: Add auto-trim command**

Calls `AutoTrimmer.Trim()` on selected segments. Same logic as TUI version.

- [ ] **Step 4: Commit**

---

### Task 9: Polish & Keyboard Shortcuts

**Files:**
- Modify: `src/Recap.Desktop/Views/MainWindow.axaml` — KeyBindings
- Modify: `src/Recap.Desktop/Views/MainWindow.axaml.cs` — keyboard routing

- [ ] **Step 1: Add all KeyBindings**

Map all 20+ keyboard shortcuts from requirements to VM commands via XAML KeyBindings.

- [ ] **Step 2: First-run wizard**

On first launch (no API key), show SettingsWindow as mandatory modal.

- [ ] **Step 3: Crash recovery prompt**

On startup, if recovered session exists, show dialog asking to recover.

- [ ] **Step 4: Open in Explorer (F key)**

```csharp
Process.Start("explorer.exe", $"/select,\"{path}\"");
```

- [ ] **Step 5: Self-update (U key)**

Show progress dialog, call AppUpdater.

- [ ] **Step 6: Commit**

---

### Task 10: CI/CD & Release

**Files:**
- Modify: `.github/workflows/ci.yml` — update build paths
- Modify: `.github/workflows/release.yml` — update publish path
- Modify: `AGENTS.md` — update for Avalonia

- [ ] **Step 1: Update CI workflow**

Change build/test commands to reference new solution structure.

- [ ] **Step 2: Update release workflow**

Change `dotnet publish` to target `src/Recap.Desktop`. Update vpk pack `--mainExe Recap.exe`.

- [ ] **Step 3: Update AGENTS.md**

Remove Spectre.Console references. Add Avalonia notes.

- [ ] **Step 4: Final build + test + publish verification**

```bash
dotnet build
dotnet test
dotnet publish src/Recap.Desktop -c Release -r win-x64 --self-contained true -o publish
```

- [ ] **Step 5: Tag v1.0.0 and push**

```bash
git tag v1.0.0
git push origin main --tags
```

- [ ] **Step 6: Create release with polished notes**

---

## Verification

After all tasks:
- `dotnet build` — zero errors
- `dotnet test` — all tests pass
- App launches, shows main window with toolbar/segments/waveform/status
- Record a segment → appears in list with waveform
- Play segment → audio plays
- Trim → waveform updates
- Transcribe → transcript window shows result
- Settings → saves and persists
- All keyboard shortcuts work
- Self-update check works
- CI builds and produces release artifact
