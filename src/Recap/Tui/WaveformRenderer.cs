using System.Collections.Concurrent;
using System.Text;
using Recap.Logging;
using Spectre.Console;

namespace Recap.Tui;

public static class WaveformRenderer
{
    private static readonly char[] Bars = { ' ', '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };

    private static readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    private record CacheEntry(float[] Peaks, long FileSize, DateTime LastModified);

    // --- Plain text rendering (for tests / simple output) ---

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

    // --- Colored markup rendering (green → yellow → red by amplitude) ---

    public static string RenderColored(float[] peaks, int width)
    {
        var sb = new StringBuilder();
        int count = Math.Min(peaks.Length, width);
        for (int i = 0; i < count; i++)
        {
            var level = Math.Clamp(peaks[i], 0f, 1f);
            int idx = (int)(level * (Bars.Length - 1));
            var color = GetAmplitudeColor(level);
            sb.Append($"[{color}]{Bars[idx]}[/]");
        }
        return sb.ToString();
    }

    // --- Half-block two-row rendering (2× vertical resolution + color) ---
    // Returns (topRow, bottomRow) as Spectre markup strings

    public static (string Top, string Bottom) RenderHalfBlock(float[] peaks, int width)
    {
        var topSb = new StringBuilder();
        var botSb = new StringBuilder();
        int count = Math.Min(peaks.Length, width);

        for (int i = 0; i < count; i++)
        {
            var level = Math.Clamp(peaks[i], 0f, 1f);
            var color = GetAmplitudeColor(level);

            // Map to 0-16 range (two rows of 8 levels each)
            int fullLevel = (int)(level * 16);

            if (fullLevel == 0)
            {
                topSb.Append(' ');
                botSb.Append(' ');
            }
            else if (fullLevel <= 8)
            {
                // Bottom row only
                topSb.Append(' ');
                int botIdx = Math.Clamp(fullLevel, 0, 8);
                botSb.Append($"[{color}]{Bars[botIdx]}[/]");
            }
            else
            {
                // Bottom row full, top row partial
                botSb.Append($"[{color}]█[/]");
                int topIdx = Math.Clamp(fullLevel - 8, 0, 8);
                topSb.Append($"[{color}]{Bars[topIdx]}[/]");
            }
        }

        return (topSb.ToString(), botSb.ToString());
    }

    // --- Peak data caching (shared by all render modes) ---

    public static float[] GetOrLoadPeaks(string filePath, int width)
    {
        if (_cache.TryGetValue(filePath, out var cached))
        {
            try
            {
                var info = new FileInfo(filePath);
                if (info.Exists && info.Length == cached.FileSize && info.LastWriteTimeUtc == cached.LastModified)
                    return ResamplePeaks(cached.Peaks, width);
            }
            catch
            {
                return ResamplePeaks(cached.Peaks, width);
            }
        }

        var peaks = LoadPeaksFromDisk(filePath);

        if (peaks == null)
        {
            // Retry up to 3 times for transient locks
            for (int retry = 0; retry < 2 && peaks == null; retry++)
            {
                Thread.Sleep(50);
                peaks = LoadPeaksFromDisk(filePath);
            }
            if (peaks == null)
            {
                Log.Warn($"Peak load failed after 3 attempts: {filePath}");
                return new float[width];
            }
        }

        try
        {
            var info = new FileInfo(filePath);
            if (info.Exists)
                _cache[filePath] = new CacheEntry(peaks, info.Length, info.LastWriteTimeUtc);
        }
        catch { }

        return ResamplePeaks(peaks, width);
    }

    // Convenience: get sparkline string (colored) for a segment
    public static string GetOrRenderSegment(string filePath, int width)
    {
        var peaks = GetOrLoadPeaks(filePath, width);
        return Render(peaks, width);
    }

    public static void InvalidateCache(string filePath)
    {
        _cache.TryRemove(filePath, out _);
    }

    public static void ClearCache()
    {
        _cache.Clear();
    }

    // Keep for tests
    public static string RenderSegment(string filePath, int width) => GetOrRenderSegment(filePath, width);

    // --- Internal ---

    private static string GetAmplitudeColor(float level) => level switch
    {
        < 0.25f => "green",
        < 0.50f => "cyan",
        < 0.70f => "yellow",
        < 0.85f => "rgb(255,165,0)",
        _ => "red"
    };

    private static float[] ResamplePeaks(float[] raw, int width)
    {
        if (raw.Length == width) return raw;
        if (raw.Length == 0) return new float[width];

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

    private static float[]? LoadPeaksFromDisk(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new NAudio.Wave.WaveFileReader(stream);
            int totalSamples = (int)(reader.Length / reader.WaveFormat.BlockAlign);
            if (totalSamples == 0) return Array.Empty<float>();

            // Store at high resolution (1 peak per ~10ms window) for flexible resampling
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

            // Normalize using median of non-silent peaks
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
