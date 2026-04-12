using NAudio.Wave;
using Recap.Models;

namespace Recap.Audio;

public class AudioEngine : IDisposable
{
    public const int SampleRate = 16000;
    public const int BitsPerSample = 16;
    public const int Channels = 1;

    private static readonly WaveFormat CaptureFormat = new(SampleRate, BitsPerSample, Channels);

    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private string? _currentFile;
    private DateTime _recordStart;
    private readonly object _lock = new();

    // Ring buffer for live waveform
    private readonly float[] _ringBuffer = new float[SampleRate * 2]; // 2 seconds
    private int _ringPos;

    public bool IsRecording { get; private set; }

    public string StartRecording(string outputPath)
    {
        if (IsRecording)
            throw new InvalidOperationException("Already recording");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        _currentFile = outputPath;
        _writer = new WaveFileWriter(outputPath, CaptureFormat);
        _recordStart = DateTime.Now;

        _waveIn = new WaveInEvent
        {
            WaveFormat = CaptureFormat,
            BufferMilliseconds = 50
        };
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.StartRecording();
        IsRecording = true;

        return outputPath;
    }

    public Segment StopRecording()
    {
        if (!IsRecording || _waveIn == null || _writer == null)
            throw new InvalidOperationException("Not recording");

        _waveIn.StopRecording();
        _waveIn.DataAvailable -= OnDataAvailable;
        _waveIn.Dispose();
        _waveIn = null;

        var duration = DateTime.Now - _recordStart;
        _writer.Flush();
        _writer.Dispose();
        _writer = null;

        IsRecording = false;

        return new Segment
        {
            FilePath = _currentFile!,
            Duration = duration,
            RecordedAt = _recordStart
        };
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_lock)
        {
            _writer?.Write(e.Buffer, 0, e.BytesRecorded);

            // Fill ring buffer for waveform
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                if (i + 1 < e.BytesRecorded)
                {
                    short sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                    _ringBuffer[_ringPos % _ringBuffer.Length] = sample / 32768f;
                    _ringPos++;
                }
            }
        }
    }

    public float[] GetWaveformSnapshot(int columns)
    {
        lock (_lock)
        {
            var result = new float[columns];
            int totalSamples = Math.Min(_ringPos, _ringBuffer.Length);
            if (totalSamples == 0) return result;

            int samplesPerCol = Math.Max(1, totalSamples / columns);
            int startPos = _ringPos - totalSamples;

            for (int col = 0; col < columns; col++)
            {
                float peak = 0;
                int colStart = startPos + col * samplesPerCol;
                for (int s = 0; s < samplesPerCol && colStart + s < _ringPos; s++)
                {
                    int idx = (colStart + s) % _ringBuffer.Length;
                    if (idx < 0) idx += _ringBuffer.Length;
                    peak = Math.Max(peak, Math.Abs(_ringBuffer[idx]));
                }
                result[col] = peak;
            }
            return result;
        }
    }

    // Playback
    private WaveOutEvent? _waveOut;
    private AudioFileReader? _playbackReader;

    public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;
    public bool IsPaused => _waveOut?.PlaybackState == PlaybackState.Paused;

    public void Play(string filePath)
    {
        StopPlayback();
        _playbackReader = new AudioFileReader(filePath);
        _waveOut = new WaveOutEvent();
        _waveOut.Init(_playbackReader);
        _waveOut.Play();
    }

    public void TogglePause()
    {
        if (_waveOut == null) return;
        if (_waveOut.PlaybackState == PlaybackState.Playing)
            _waveOut.Pause();
        else if (_waveOut.PlaybackState == PlaybackState.Paused)
            _waveOut.Play();
    }

    public void StopPlayback()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;
        _playbackReader?.Dispose();
        _playbackReader = null;
    }

    // PCM splicing
    public static void SpliceSegments(IEnumerable<string> files, string outputPath)
    {
        using var writer = new WaveFileWriter(outputPath, CaptureFormat);
        foreach (var file in files)
        {
            using var reader = new WaveFileReader(file);
            var buffer = new byte[reader.Length];
            int read = reader.Read(buffer, 0, buffer.Length);
            writer.Write(buffer, 0, read);
        }
    }

    public void Dispose()
    {
        StopPlayback();
        if (IsRecording)
        {
            try { StopRecording(); } catch { }
        }
        GC.SuppressFinalize(this);
    }
}
