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
    double PlaybackPosition { get; }
}
