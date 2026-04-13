using System.Collections.Concurrent;
using Recap.Logging;

namespace Recap.Tui;

public static class WaveformRenderer
{
    private static readonly char[] Bars = { ' ', '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };

    private static readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    private record CacheEntry(string Sparkline, long FileSize, DateTime LastModified, int Width);

    public static string Render(float[] peaks, int width)
    {
        var chars = new char[Math.Min(peaks.Length, width)];
        for (int i = 0; i < chars.Length; i++)
        {
            var level = Math.Clamp(peaks[i], 0f, 1f);
            int idx = (int)(level * (Bars.Length - 1));
            chars[i] = Bars[idx];
        }
        return new string(chars);
    }

    public static string GetOrRenderSegment(string filePath, int width)
    {
        if (_cache.TryGetValue(filePath, out var cached) && cached.Width == width)
        {
            try
            {
                var info = new FileInfo(filePath);
                if (info.Exists && info.Length == cached.FileSize && info.LastWriteTimeUtc == cached.LastModified)
                    return cached.Sparkline;
            }
            catch
            {
                return cached.Sparkline;
            }
        }

        var sparkline = RenderSegmentFromDisk(filePath, width);

        // Retry up to 3 times on failure, then default to empty
        if (sparkline.Contains('?'))
        {
            for (int retry = 0; retry < 2; retry++)
            {
                Thread.Sleep(50);
                sparkline = RenderSegmentFromDisk(filePath, width);
                if (!sparkline.Contains('?')) break;
            }
            if (sparkline.Contains('?'))
            {
                Log.Warn($"Sparkline failed after 3 attempts: {filePath}");
                sparkline = new string(' ', width);
            }
        }

        try
        {
            var info = new FileInfo(filePath);
            if (info.Exists)
                _cache[filePath] = new CacheEntry(sparkline, info.Length, info.LastWriteTimeUtc, width);
        }
        catch { }

        return sparkline;
    }

    public static void InvalidateCache(string filePath)
    {
        _cache.TryRemove(filePath, out _);
    }

    public static void ClearCache()
    {
        _cache.Clear();
    }

    public static string RenderSegment(string filePath, int width) => GetOrRenderSegment(filePath, width);

    private static string RenderSegmentFromDisk(string filePath, int width)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new NAudio.Wave.WaveFileReader(stream);
            var samples = new List<float>();
            var buffer = new byte[2];
            while (reader.Read(buffer, 0, 2) == 2)
            {
                short sample = (short)(buffer[0] | (buffer[1] << 8));
                samples.Add(Math.Abs(sample / 32768f));
            }

            if (samples.Count == 0) return new string(' ', width);

            var peaks = new float[width];
            int samplesPerCol = Math.Max(1, samples.Count / width);
            for (int col = 0; col < width; col++)
            {
                float peak = 0;
                int start = col * samplesPerCol;
                for (int s = 0; s < samplesPerCol && start + s < samples.Count; s++)
                    peak = Math.Max(peak, samples[start + s]);
                peaks[col] = peak;
            }

            // Normalize using median of non-silent peaks
            var nonSilent = peaks.Where(p => p > 0.001f).OrderBy(p => p).ToArray();
            if (nonSilent.Length > 0)
            {
                float median = nonSilent[nonSilent.Length / 2];
                float normFactor = median / 0.6f;
                for (int i = 0; i < peaks.Length; i++)
                    peaks[i] = Math.Min(1f, peaks[i] / normFactor);
            }

            return Render(peaks, width);
        }
        catch (Exception ex)
        {
            Log.Warn($"Sparkline render failed for {filePath}: {ex.Message}");
            return new string('?', width);
        }
    }
}
