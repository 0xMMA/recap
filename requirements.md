# Requirements — Recap

> Single-purpose voice capture tool for DM session recaps.
> Record spoken recaps in chunks, trim dead air, transcribe via ElevenLabs Scribe v2, copy to clipboard.
> "Audacity but brutally scope-limited to what you actually need."

---

## Core Purpose

Record spoken DM session recaps in arbitrary-length chunks, trim dead air, and get a raw German transcript on the clipboard — without bloat, without the mouse.

## Target Platform

- **v1 (TUI, shipped):** Windows x64, .NET 10, self-contained single-file exe
- **v2 (GUI, planned):** Avalonia 11, cross-platform (Windows/Linux/macOS)

---

## Functional Requirements

### R1–R4: Recording & Segments

- **R1.** Record button toggles recording start/stop. Push-to-talk mode (config flag) makes it hold-to-record.
- **R2.** Each start→stop cycle produces exactly one ordered segment in the session list.
- **R3.** All segments captured as mono PCM WAV, 16 kHz, 16-bit. No user-facing format options.
- **R4.** Spliced output = raw PCM concat of all segments in order. No ffmpeg dependency.

### R5–R7: Segment List & Selection

- **R5.** UI shows segment list: index, duration, inline waveform visualization.
- **R6.** Delete removes selected segment(s). Single-level undo restores last deletion.
- **R7.** Multi-select (shift+click or shift+arrow) for bulk delete and bulk transcribe.
- **R21.** *(Added v0.3.0)* Select-all shortcut for instant full selection.

### R8–R9: Live Feedback & Playback

- **R8.** While recording, a live waveform visualization updates in real-time (≥10 fps).
- **R9.** Play selected segment. Play full spliced audio. Pause/resume during playback. Play and pause controls never conflated with record.

### R10–R11: Trimming

- **R10.** Manual trim mode: waveform view, two markers (left/right), arrow keys to move, Tab to switch, Enter to confirm. Result replaces segment in place.
- **R11.** Auto-trim: RMS-based silence detection. Noise floor from first 300ms, min silence 600ms, 200ms padding. Defensive bail if noise floor > -30 dBFS.

### R12–R14, R16: Save & Transcribe

- **R12.** Save dialog: spliced single file vs individual segments, folder picker. Default filename `session-YYYY-MM-DD-HHmm[-NN].wav`.
- **R13.** Transcribe selection. Empty selection → full spliced audio.
- **R14.** Default language set in first-run wizard (German). Language toggle cycles auto → default → manual pick.
- **R16.** After transcription, result panel shows text with actions: Copy, Save .md, Save .txt, Append to file, Discard.
- **R22.** *(Added v0.3.0)* Transcribe-all shortcut: splices everything, single API call, combined output. No segment separators in output.
- **R23.** *(Added v0.3.0)* Result view is scrollable (up/down/pgup/pgdn/home/end). Keybind footer always visible.

### R15, R17, R20: Settings & Session

- **R15.** Settings panel: API key, model (scribe_v2), default language, audio events on/off, timestamp granularity. Persisted to config.json. Diarization hardcoded false (solo speaker).
- **R17.** Save/transcribe does not clear session. New session with confirmation if unsaved.
- **R20.** No transcript editing in-app. No formatting. No LLM calls beyond Scribe.

### R18–R19: Crash Safety & Status

- **R18.** Each segment flushed to temp dir immediately on stop. Crash never loses more than in-flight segment.
- **R19.** Status bar always visible: recording state, active language, segment count, total duration, API key status, last action result.
- **R24.** *(Added v0.3.0)* Missing file indicator: segments with deleted/missing WAV files show visual warning. Play/transcribe skip missing files gracefully.

### R25–R28: Additional Capabilities (Added post-v1)

- **R25.** *(v0.2.0)* Open segment location in system file explorer.
- **R26.** *(v0.2.1)* Single-file portable executable. No installer required — extract and run.
- **R27.** *(v0.2.0)* Self-update from GitHub Releases (Velopack). Check on startup, manual trigger.
- **R28.** *(v0.4.0)* File log at config dir. Rotates at 1MB. Logs startup, recording, transcription, all errors.

---

## Non-Goals (Hard Scope Limits)

- Transcript editing, formatting, or LLM post-processing
- Multi-track audio, effects, EQ, noise reduction
- Real-time streaming transcription — batch only
- Cloud sync, accounts, multi-user
- Mobile
- Anything beyond audio → transcript → clipboard/file

---

## Technical Constraints & Lessons Learned

### File I/O
- All file reads MUST use `FileShare.ReadWrite` — prevents locking crashes between concurrent readers (UI waveform, playback, recording)
- File delete/replace operations MUST retry 3× with 100ms delay — antivirus and stale handles cause transient locks
- Never cache error results (e.g. failed sparkline renders) — retry on next access

### Error Handling
- Never crash on recoverable errors — show in status bar / log instead
- API errors must show actual response body, not generic status codes
- File-not-found during splice/play: skip missing files, report count, continue

### Concurrency
- Transcription must be async/non-blocking — UI stays responsive during API calls
- Recording uses dedicated capture thread (NAudio WaveInEvent → ring buffer)
- Playback on separate thread, posts position updates to UI
- Waveform rendering cached — no file I/O in render loop for stable segments

### Audio Format
- Fixed: mono PCM WAV, 16 kHz, 16-bit
- Splicing = raw PCM concat after stripping WAV headers, write one new header
- Segment filenames: timestamp-based (HHmmss-fff.wav) to avoid collisions after deletes

### Normalization (for waveform display)
- Median-based normalization of non-silent peaks — robust against loud pops and silence gaps
- Median maps to ~60% display height; peaks above ~1.7× median clip to max

---

## Architecture (Current — TUI)

```
Program.cs → TuiHost (Spectre.Console Live loop)
  ├── SegmentListPanel (IRenderable) ← SessionState
  ├── StatusBar (IRenderable)
  ├── WaveformRenderer (cached sparklines)
  ├── TrimMode (interactive two-marker editor)
  ├── SaveDialog / SettingsDialog / ResultPanel (modal, exit Live)
  ├── AudioEngine (NAudio: capture, playback, splice)
  ├── AutoTrimmer (RMS silence detection)
  ├── ScribeClient (ElevenLabs API)
  ├── AppUpdater (Velopack + GitHub Releases)
  ├── SessionPersistence (crash recovery, temp dir, manifest)
  ├── AppConfig (JSON, %APPDATA%)
  └── Log (file logger, %APPDATA%/recap.log)
```

### Key Components

| Component | Responsibility |
|---|---|
| `AudioEngine` | Mic capture, WAV write, playback, PCM concat, ring buffer for live waveform |
| `SessionState` | Segment list, selection, multi-select, single-level undo, scroll offset |
| `ScribeClient` | ElevenLabs Scribe v2 multipart POST, error detail parsing |
| `AutoTrimmer` | RMS windowed silence detection, noise floor safety |
| `AppConfig` | JSON config persistence, first-run wizard data |
| `SessionPersistence` | Temp dir per session, manifest JSON, crash recovery, prune old sessions |
| `AppUpdater` | Velopack UpdateManager + GithubSource, check/download/apply/restart |
| `Log` | File logger, %APPDATA%/recap.log, rotate at 1MB |
| `Segment` | Model: index, filepath, duration, cached HasFile |

### Test Coverage (52 tests)

- `SessionState`: add, delete, undo, selection, multi-select, select-all, clear, total duration
- `AudioEngine`: PCM splice correctness, format preservation, sample order
- `AutoTrimmer`: RMS computation, silence detection, noise floor safety bail
- `TuiRenderTests`: SegmentListPanel output, StatusBar output, WaveformRenderer cache + normalization
- All tests use xUnit + Shouldly + Spectre.Console.Testing

---

## Resolved Decisions

1. Diarization off, not in UI. Solo-speaker use case.
2. No global hotkey. App must be focused to record.
3. Session persistence: keep last 3 session temp folders, prune on startup.
4. Push-to-talk via Win32 GetAsyncKeyState (Windows-only).
5. Self-update via Velopack portable zip from GitHub Releases.
6. Logging to file, not console — TUI owns stdout.
7. Single-file publish via PublishSingleFile + IncludeNativeLibrariesForSelfExtract.

---

## CI/CD

- **CI:** GitHub Actions, runs on push/PR to main. Build + test on `windows-latest`.
- **Release:** Tag `v*` triggers build → `dotnet publish` → `vpk pack --noInst` → `gh release create --draft` with portable zip.
- **Versioning:** Semver. New version on every release. Never retag.
- **Release notes:** Polished markdown with features, fixes, upgrade instructions, changelog link.
