# PLAN.md — Recap

> Single-purpose voice capture tool. Audio in, raw transcript out. Built for DM session recaps.
> Stack: .NET 10 console app, Spectre.Console (TUI), NAudio (capture/playback), ElevenLabs Scribe v2 (STT). **Windows-only, `win-x64` self-contained.**

---

## Question

How do I record spoken DM session recaps in arbitrary-length chunks, trim dead air, and get a raw German transcript on the clipboard — without leaving the terminal and without the bloat of Audacity?

## Non-goals

- Transcript editing, formatting, or LLM post-processing.
- Multi-track audio, effects, EQ, noise reduction.
- Real-time streaming transcription. Batch only.
- Cloud sync, accounts, multi-user.
- GUI. TUI only.
- Mobile.
- Anything not directly required by the 20 requirements below.

---

## Requirements (acceptance contract)

The implementation is complete when **every** item below can be demonstrated end-to-end. Each is testable.

### Recording & segments

- [ ] **R1.** `R` toggles recording start/stop. Push-to-talk mode (config flag) makes `R` hold-to-record.
- [ ] **R2.** Each start→stop cycle produces exactly one ordered segment in the session list.
- [ ] **R3.** All segments captured as mono PCM WAV, 16 kHz, 16-bit. No user-facing format options in v1.
- [ ] **R4.** Spliced output = raw PCM concat of all segments in order. No ffmpeg dependency in v1.
- [ ] **R18.** Each segment flushed to `%TEMP%\recap\<session-id>\NN.wav` immediately on stop. Crash never loses more than the in-flight segment.

### Segment list & editing

- [ ] **R5.** TUI shows segment list: index, duration, inline waveform sparkline.
- [ ] **R6.** `Delete` / `Backspace` removes selected segment. `Ctrl+Z` restores the last deletion (single-level undo).
- [ ] **R7.** `Shift+↑/↓` extends selection for bulk delete and bulk transcribe.
- [ ] **R10.** `T` opens trim mode for the selected segment: waveform view, two markers (`←/→` to move, `Tab` to switch, `Enter` to confirm). Result replaces the segment in place.
- [ ] **R11.** `A` runs auto-trim on selection. Defaults: noise floor estimated from first 300 ms, min silence 600 ms, 200 ms padding around speech. Never runs unprompted.

### Live feedback

- [ ] **R8.** While recording, a scrolling waveform strip is rendered at the bottom of the screen (≥10 fps).
- [ ] **R19.** Status bar always visible: recording state, active language, segment count, total duration, API key status, last action result.

### Playback

- [ ] **R9.** `Enter` plays selected segment. `Shift+Enter` plays full spliced audio. `Space` toggles play/pause **only during playback** — never conflated with stop-recording.

### Save & transcribe

- [ ] **R12.** `S` opens save dialog: choose *spliced single file* vs *individual segments*, plus folder. Default filename `session-YYYY-MM-DD-HHmm[-NN].wav`.
- [ ] **R13.** `X` transcribes current selection. Empty selection → full spliced audio. Multi-select → per-segment, joined with segment markers.
- [ ] **R14.** Default language set in first-run wizard (German for primary user). Status-bar field shows the *active* language for the next call. `L` cycles `auto` → default → manual pick without leaving the main view.
- [ ] **R16.** After transcription, result panel shows text. `C` / `Ctrl+C` copies to clipboard. `Enter` opens action menu: Copy, Save .md, Save .txt, Append to file, Discard.

### Settings & session

- [ ] **R15.** `Ctrl+,` opens settings panel: API key, model (`scribe_v2`), default language, audio events on/off, timestamp granularity. Persisted to `%APPDATA%\recap\config.json`. Diarization is hardcoded `false` (solo speaker) and not exposed in the UI. No other knobs.
- [ ] **R17.** Saving/transcribing does **not** clear the session. `N` starts a new session, with confirmation prompt if unsaved segments exist.
- [ ] **R20.** No transcript editing in-app. No formatting. No LLM calls beyond Scribe. Anything beyond audio→transcript→clipboard/file is out of scope.

---

## Architecture

```
┌─────────────────────────────────────────────────────┐
│ Spectre.Console TUI (Live + Layout)                 │
│  ┌────────────┐  ┌──────────────────┐  ┌─────────┐  │
│  │ Segments   │  │ Waveform / Trim  │  │ Result  │  │
│  │ list       │  │ view             │  │ panel   │  │
│  └────────────┘  └──────────────────┘  └─────────┘  │
│  Status bar (always)                                │
└─────────────────────────────────────────────────────┘
        │              │                    │
        ▼              ▼                    ▼
   SessionState   AudioEngine          ScribeClient
   (segments,     (NAudio capture/      (HttpClient,
    selection,     playback, WAV I/O,    multipart POST,
    undo)          PCM concat, trim)     language opt)
        │              │                    │
        └──────┬───────┴────────────────────┘
               ▼
         Persistence (temp WAVs, config.json)
```

### Key components

| Component | Responsibility | Lib |
|---|---|---|
| `AudioEngine` | Mic capture, WAV write, playback, PCM concat, silence detection | NAudio |
| `SessionState` | Segment list, selection, undo, dirty flag | — |
| `ScribeClient` | ElevenLabs STT API call, multipart upload | `HttpClient` |
| `TuiHost` | Spectre layout, Live rendering, key dispatch | Spectre.Console |
| `KeyMap` | Centralized binding table, mode-aware (normal/trim/playback) | — |
| `Config` | Load/save JSON, first-run wizard | `System.Text.Json` |

### Threading model

- **Capture thread**: NAudio `WaveInEvent` → ring buffer → flushed to disk on stop.
- **UI thread**: Spectre `Live` loop, ~15 fps. Reads ring buffer for waveform strip.
- **Transcribe**: `async`/`await`, status spinner via `AnsiConsole.Status()`.
- **Playback**: NAudio `WaveOutEvent` on its own thread, posts position updates to UI.

No locks beyond `lock` around the ring buffer and segment list. No `Task.Run` unless required.

---

## Solution sketch — critical paths

### Splicing (R4)
Same format for all segments → concat is byte-level after stripping headers, then write one new WAV header. ~30 lines. No ffmpeg.

### Auto-trim (R11)
1. Compute RMS over 20 ms windows.
2. Noise floor = mean RMS of first 15 windows.
3. Speech threshold = noise floor × 4 (configurable).
4. Mark windows below threshold for ≥600 ms as silence.
5. Keep 200 ms padding on each side of every speech run.
6. Drop silence regions, concat speech regions.

Defensive: never trim if estimated noise floor > -30 dBFS (mic too hot or noisy environment — bail and tell the user).

### Live waveform (R8)
Downsample ring buffer to N columns (terminal width). Map peak amplitude per column to a vertical bar character (`▁▂▃▄▅▆▇█`). Render via `Live` at 15 fps. No allocations in the hot path — reuse a `char[]`.

### Trim mode (R10)
Render the segment's full waveform downsampled to terminal width. Two markers as colored vertical bars. `←/→` moves selected marker by 1 sample-column; `Shift+←/→` by 10. `Tab` swaps active marker. `Enter` slices the underlying PCM and replaces the segment.

### Transcribe (R13)
```
POST https://api.elevenlabs.io/v1/speech-to-text
multipart/form-data:
  file=<wav bytes>
  model_id=scribe_v2
  language_code=<active lang or omitted for auto>
  tag_audio_events=<bool>
```
Response → text → result panel. Diarization flag never sent (hardcoded off).

---

## Plan (implementation order)

Each step is independently testable. Don't move to step N+1 until step N is demonstrably working.

| # | Step | Done when |
|---|---|---|
| 1 | Project scaffold: .NET 10 console, Spectre.Console, NAudio, xUnit + Shouldly test project | `dotnet run` shows a Spectre layout with status bar |
| 2 | `AudioEngine.StartRecord()` / `StopRecord()` writing a fixed-format WAV to disk | Manual: record 5s, play resulting file in any player |
| 3 | `SessionState` with segment list, selection, undo. Unit tests with Shouldly | Tests green: add, select, delete, undo |
| 4 | TUI: segment list panel + status bar + key dispatch (`R`, `Delete`, `↑/↓`, `Ctrl+Z`) | Record → segment appears → delete → undo |
| 5 | PCM concat splicing + `S` save dialog (spliced vs segments) | Saved files play correctly end-to-end |
| 6 | Playback (`Enter`, `Shift+Enter`, `Space` pause). Mode-aware key map | Selected segment plays; spliced plays; pause works |
| 7 | Live waveform strip during recording | Visible bars while talking, flat when silent |
| 8 | `ScribeClient` + `X` transcribe selection. Result panel. Hardcoded German for now | Real transcript from ElevenLabs appears |
| 9 | `Ctrl+,` settings panel + first-run wizard + `config.json` persistence | API key, language survive restart |
| 10 | Language toggle `L` in status bar (auto / default / pick) | Toggle changes language sent in next call |
| 11 | Result panel actions: `C` copy, `Enter` action menu (Copy/Save .md/.txt/Append/Discard) | Clipboard works on Windows; files saved |
| 12 | Manual trim mode `T` with two markers | Trimmed segment replaces original, plays correctly |
| 13 | Auto-trim `A` with defensive noise-floor check | Silence-heavy recording shrinks, speech preserved |
| 14 | Multi-select `Shift+↑/↓` + bulk delete + bulk transcribe | Multiple segments transcribed and joined |
| 15 | Push-to-talk mode toggle | Hold `R` records, release stops |
| 16 | Crash-safety: temp folder per session, recovery on next launch | Kill app mid-recording, relaunch, segments still there |
| 17 | Polish: status bar dirty flag, confirm prompts, error handling for missing API key, mic permission errors | Manual smoke test of all 20 requirements |

**Stretch (post-v1, only if v1 ships):**
- Append-to-file mode for continuous transcript building across sessions.

---

## Risks & mitigations

| Risk | Mitigation |
|---|---|
| NAudio mic device selection edge cases (USB headsets, default device changes) | First-run wizard picks device explicitly; show device name in status bar |
| Spectre `Live` flicker at high update rates | Cap waveform redraw at 15 fps, use `LiveRenderable` not full-layout rebuilds |
| Clipboard on Windows console apps | Use `TextCopy` NuGet, not `System.Windows.Forms` |
| ElevenLabs API key in config.json | DPAPI-encrypt on Windows; document plain-text fallback for other OSes |
| Auto-trim eating quiet speech | Conservative defaults + bail-out if noise floor too high + always reversible (operates on copy until confirmed) |
| Long recordings exceeding Scribe non-realtime limits | Scribe allows 10h standard mode; warn at 8h |

---

## Resolved decisions

1. **Windows-only.** `win-x64` self-contained publish. NAudio/WASAPI, DPAPI for API key, `TextCopy` for clipboard. No cross-platform compromises in v1.
2. **Diarization off, not in UI.** Solo-speaker use case. Hardcoded `false`. Lives in `config.json` only for power users who hand-edit; not exposed in the settings panel.
3. **No global hotkey.** TUI must be focused to record. Avoids Win32 interop, AV false positives, and accidental capture from other apps. Revisit only if real-world use proves it necessary.
4. **Session persistence:** keep last 3 session temp folders for crash recovery; prune older on startup.

---

## Definition of done

- All 20 requirements demonstrable.
- xUnit + Shouldly test suite covers `SessionState`, `AudioEngine` (PCM concat, auto-trim math), `ScribeClient` (mocked HTTP).
- Single `dotnet publish` produces a self-contained Windows executable.
- README with: install, first-run, keybinds cheatsheet, troubleshooting (no mic, bad API key, network errors).
- A real DM session recap recorded, trimmed, transcribed, and pasted into Obsidian end-to-end without touching the mouse.
