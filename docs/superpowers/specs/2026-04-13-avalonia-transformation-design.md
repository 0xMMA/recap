# Avalonia 11 Transformation — Design Spec

## Purpose

Transform Recap from a Spectre.Console TUI into an Avalonia 11 cross-platform desktop app. Same core purpose (record → trim → transcribe → clipboard), proper GUI with real waveform rendering, keyboard-first workflow preserved.

## What Changes

| Aspect | TUI (current) | GUI (target) |
|--------|---------------|--------------|
| Framework | Spectre.Console Live loop | Avalonia 11 MVVM |
| Waveform | Unicode bar chars, 8 levels | Pixel-level Canvas rendering |
| Layout | Terminal text grid | Resizable window with panels |
| Platform | Windows-only (win-x64) | Cross-platform (Windows first, Linux/macOS stubs) |
| Input | Console.ReadKey dispatch | Avalonia KeyBindings + buttons |
| Dialogs | Exit Live → AnsiConsole prompts | Native Avalonia windows/dialogs |

## What Stays

All non-UI code carries over with minimal changes:

- `AudioEngine` — NAudio capture, playback, splice, ring buffer
- `AutoTrimmer` — RMS silence detection
- `ScribeClient` — ElevenLabs API client
- `SessionState` — segment list, selection, undo (becomes backing model for VM)
- `SessionPersistence` — crash recovery, temp dirs
- `AppConfig` — JSON config
- `AppUpdater` — Velopack self-update
- `Log` — file logger
- `Segment` model — index, filepath, duration, HasFile cache
- All 28 requirements from requirements.md

## Architecture

```
src/
  Recap.Core/                    # Extracted from current src/Recap
    Recap.Core.csproj            # netstandard2.1 or net10.0 class lib
    Audio/
      IAudioEngine.cs            # NEW: interface for cross-platform
      AudioEngine.cs             # NAudio implementation (Windows)
      AutoTrimmer.cs
    Api/ScribeClient.cs
    Config/AppConfig.cs
    Logging/Log.cs
    Models/
      Segment.cs
      AppMode.cs
      RecordingState.cs
    State/
      SessionState.cs
      SessionPersistence.cs
    Updates/AppUpdater.cs

  Recap.Desktop/                 # NEW: Avalonia 11 app
    Recap.Desktop.csproj         # net10.0, Avalonia 11
    App.axaml / App.axaml.cs
    Program.cs
    ViewModels/
      MainWindowViewModel.cs     # Binds to SessionState, commands
      SegmentViewModel.cs        # Per-segment display data
      SettingsViewModel.cs
    Views/
      MainWindow.axaml           # Main layout
      SettingsWindow.axaml       # Settings dialog
      TranscriptWindow.axaml     # Result view (scrollable, actions)
    Controls/
      WaveformControl.cs         # Custom: draws waveform via DrawingContext
      LiveWaveformControl.cs     # Custom: ring buffer → live waveform
      SegmentListControl.axaml   # Segment list item template
    Converters/
      DurationConverter.cs
      RecordingStateConverter.cs

tests/
  Recap.Tests/                   # Updated refs → Recap.Core
    Recap.Tests.csproj
    SessionStateTests.cs
    AudioEngineTests.cs
    AutoTrimmerTests.cs
    (TuiRenderTests.cs → removed, replaced by VM tests)
    ViewModelTests.cs            # NEW: test commands, state transitions
```

## UI Layout

```
┌─────────────────────────────────────────────────────────┐
│ [●REC] [▶Play] [⏸] [■Stop] [💾Save] [📝TX] [⚙]  │ Toolbar
├──────────────┬──────────────────────────────────────────┤
│ Segments     │ Waveform                                 │
│              │                                          │
│ > 01  0:05.2 │  ╱╲  ╱╲╱╲    ╱╲  ╱╲                   │
│   02  0:12.1 │ ╱  ╲╱    ╲╱╲╱  ╲╱  ╲                  │
│   03  0:03.4 │                                          │
│   04  0:08.7 │ [Trim markers when in trim mode]        │
│              │                                          │
├──────────────┴──────────────────────────────────────────┤
│ ■ IDLE │ DE │ 4 seg │ 00:29.4 │ API ✓ │ Ready         │ Status
└─────────────────────────────────────────────────────────┘
```

- **Left panel**: Segment list with index, duration, mini waveform preview
- **Center panel**: Full waveform of selected segment (or live recording waveform)
- **Toolbar**: Buttons + keyboard shortcuts (R, Space, S, X, etc.)
- **Status bar**: Same info as TUI version
- **Transcript**: Opens as overlay or separate window

## Keyboard Shortcuts (Preserved)

All TUI keybinds map directly:

| Key | Action |
|-----|--------|
| R | Record / Stop |
| ↑/↓ | Navigate segments |
| Shift+↑/↓ | Extend selection |
| Ctrl+A | Select all |
| Delete | Delete segment(s) |
| Ctrl+Z | Undo |
| Enter | Play selected |
| Shift+Enter | Play all |
| Space | Pause/Resume |
| X | Transcribe selection |
| Shift+X | Transcribe all |
| T | Trim mode |
| A | Auto-trim |
| S | Save |
| L | Cycle language |
| F | Open in Explorer |
| N | New session |
| U | Check updates |
| Ctrl+, | Settings |
| Q / Ctrl+Q | Quit |

## Waveform Rendering

Custom `WaveformControl` using Avalonia's `DrawingContext`:

- Draws actual audio waveform as filled polygon (like Audacity simplified view)
- Color: solid fill for amplitude, lighter for RMS
- Selection region: highlighted overlay
- Trim markers: draggable vertical lines
- Zoom: scroll wheel changes time scale
- Responsive: redraws on resize

Peaks cached same as TUI version — `WaveformRenderer` still computes peaks, control just draws them as pixels instead of chars.

## Cross-Platform Strategy

**Phase 1 (this transformation):** Windows. NAudio for audio. Same as TUI.

**Phase 2 (future):** `IAudioEngine` interface extracted. Platform-specific implementations:
- Windows: NAudio (existing)
- Linux: PipeWire/PulseAudio via managed wrapper
- macOS: CoreAudio via P/Invoke

WAV I/O, splicing, auto-trim all work cross-platform today (pure byte manipulation). Only capture/playback is platform-specific.

## MVVM Data Flow

```
User action (key/click)
  → ICommand on MainWindowViewModel
    → Modifies SessionState / calls AudioEngine
      → PropertyChanged notifications
        → Avalonia binding updates UI
```

- `MainWindowViewModel` wraps `SessionState` and `AudioEngine`
- `ObservableCollection<SegmentViewModel>` mirrors `SessionState.Segments`
- Recording state, language, segment count → bound properties
- Async commands for transcription (non-blocking by design)

## Settings & Dialogs

- **Settings**: Separate `SettingsWindow` (modal dialog). Same fields as TUI: API key, language, push-to-talk, audio events, timestamp granularity.
- **Save**: Native file dialog via `Avalonia.Platform.Storage` API
- **Transcript**: `TranscriptWindow` with scrollable text, Copy/Save/Append buttons, close button. No more manual scroll offset — native scrolling.

## Dependencies

| Package | Purpose |
|---------|---------|
| Avalonia 11 | UI framework |
| Avalonia.Desktop | Desktop platform support |
| Avalonia.Themes.Fluent | Modern theme |
| CommunityToolkit.Mvvm | MVVM infrastructure (ObservableObject, RelayCommand) |
| NAudio | Audio capture/playback (Windows) |
| Velopack | Self-update |
| TextCopy | Clipboard |

Remove: `Spectre.Console` (TUI), `Spectre.Console.Testing`

## Testing Strategy

- Keep all existing core tests (SessionState, AudioEngine, AutoTrimmer)
- Remove TuiRenderTests (Spectre-specific)
- Add ViewModel tests: command execution, state transitions, property notifications
- Waveform rendering: visual testing deferred (complex to automate for pixel output)

## Build & Release

- Same CI/CD workflow, target `win-x64`
- `dotnet publish` produces single-file Avalonia app
- Velopack packaging unchanged
- Version bump: 1.0.0 (major — breaking UI change)
