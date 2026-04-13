using System.Collections.Concurrent;
using Recap.Core.Logging;

namespace Recap.Core.Audio;

public static class WaveformData
{
    private static readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private record CacheEntry(float[] Peaks, long FileSize, DateTime LastModified);

    /// <summary>
    /// Load normalized peaks at ~10ms resolution. Cached by file identity.
    /// </summary>
    public static float[] GetPeaks(string filePath, int targetWidth)
    {
        if (_cache.TryGetValue(filePath, out var cached))
        {
            try
            {
                var info = new FileInfo(filePath);
                if (info.Exists && info.Length == cached.FileSize && info.LastWriteTimeUtc == cached.LastModified)
                    return Resample(cached.Peaks, targetWidth);
            }
            catch { return Resample(cached.Peaks, targetWidth); }
        }

        var peaks = LoadFromDisk(filePath);
        if (peaks == null)
        {
            for (int retry = 0; retry < 2 && peaks == null; retry++)
            {
                Thread.Sleep(50);
                peaks = LoadFromDisk(filePath);
            }
            if (peaks == null)
            {
                Log.Warn($"Peak load failed after 3 attempts: {filePath}");
                return new float[targetWidth];
            }
        }

        try
        {
            var info = new FileInfo(filePath);
            if (info.Exists)
                _cache[filePath] = new CacheEntry(peaks, info.Length, info.LastWriteTimeUtc);
        }
        catch { }

        return Resample(peaks, targetWidth);
    }

    public static void Invalidate(string filePath) => _cache.TryRemove(filePath, out _);
    public static void Clear() => _cache.Clear();

    private static float[] Resample(float[] raw, int width)
    {
        if (raw.Length == 0) return new float[width];
        if (raw.Length == width) return raw;
        var result = new float[width];
        float ratio = (float)raw.Length / width;
        for (int i = 0; i < width; i++)
        {
            int start = (int)(i * ratio);
            int end = Math.Min((int)((i + 1) * ratio), raw.Length);
            float peak = 0;
            for (int j = start; j < end; j++)
                peak = Math.Max(peak, raw[j]);
            result[i] = peak;
        }
        return result;
    }

    private static float[]? LoadFromDisk(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new NAudio.Wave.WaveFileReader(stream);
            int totalSamples = (int)(reader.Length / reader.WaveFormat.BlockAlign);
            if (totalSamples == 0) return Array.Empty<float>();

            int windowSize = Math.Max(1, reader.WaveFormat.SampleRate / 100);
            int peakCount = Math.Max(1, totalSamples / windowSize);
            var peaks = new float[peakCount];
            var buffer = new byte[2];

            for (int p = 0; p < peakCount; p++)
            {
                float peak = 0;
                for (int s = 0; s < windowSize; s++)
                {
                    if (reader.Read(buffer, 0, 2) != 2) break;
                    short sample = (short)(buffer[0] | (buffer[1] << 8));
                    peak = Math.Max(peak, Math.Abs(sample / 32768f));
                }
                peaks[p] = peak;
            }

            // Median normalization
            var nonSilent = peaks.Where(p => p > 0.001f).OrderBy(p => p).ToArray();
            if (nonSilent.Length > 0)
            {
                float median = nonSilent[nonSilent.Length / 2];
                float normFactor = median / 0.6f;
                for (int i = 0; i < peaks.Length; i++)
                    peaks[i] = Math.Min(1f, peaks[i] / normFactor);
            }
            return peaks;
        }
        catch (Exception ex)
        {
            Log.Warn($"Peak load failed for {filePath}: {ex.Message}");
            return null;
        }
    }
}
