# Recap

Single-purpose voice capture tool for DM session recaps. Record spoken recaps in chunks, trim dead air, transcribe via ElevenLabs Scribe v2, copy to clipboard.

Like Audacity, but brutally scope-limited to what you actually need.

## Install

1. Download `Recap-win-Portable.zip` from [Releases](https://github.com/0xMMA/recap/releases)
2. Extract `Recap.exe`
3. Run it
4. Enter your [ElevenLabs](https://elevenlabs.io) API key on first launch

**Requirements:** Windows x64

## Features

- **Record** segments with R (toggle or push-to-talk)
- **Waveform** — pixel-level audio visualization with zoom (scroll wheel), pan, and playback position tracking
- **Trim** — drag markers on waveform, or auto-trim silence with RMS detection
- **Cut** — mouse-select a portion on the waveform, Delete to remove it
- **Transcribe** — ElevenLabs Scribe v2. Per-segment or all-at-once. Copy/save result.
- **Save** — spliced single WAV or individual segments
- **Crash recovery** — segments saved to temp dir, recovered on next launch
- **Self-update** — press U to check for new versions

## Keyboard Shortcuts

| Key | Action | Key | Action |
|-----|--------|-----|--------|
| R | Record/Stop | X | Transcribe |
| ↑/↓ | Navigate segments | Shift+X | Transcribe all |
| Shift+↑/↓ | Multi-select | T | Trim mode |
| Ctrl+A | Select all | A | Auto-trim |
| Delete | Delete segment/selection | S | Save |
| Ctrl+Z | Undo | L | Cycle language |
| Enter | Play selected | F | Open in Explorer |
| Shift+Enter | Play all | N | New session |
| Space | Pause/Resume | U | Check updates |
| Scroll wheel | Zoom waveform | Ctrl+, | Settings |
| Q | Quit | | |

## Build

```bash
dotnet build
dotnet test
dotnet publish src/Recap.Desktop -c Release -r win-x64 --self-contained true -o publish
```

## Project Structure

```
src/Recap.Core/      — Audio engine, API client, state, config, logging
src/Recap.Desktop/   — Avalonia 11 UI (MVVM, views, custom controls)
tests/Recap.Tests/   — xUnit + Shouldly
```

## Tech Stack

- .NET 10
- Avalonia 11 (Fluent dark theme)
- NAudio (audio capture/playback)
- ElevenLabs Scribe v2 (speech-to-text)
- Velopack (self-update)
- CommunityToolkit.Mvvm

## License

[MIT + Commons Clause](LICENSE)
