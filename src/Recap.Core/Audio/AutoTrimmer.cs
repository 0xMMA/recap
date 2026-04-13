using NAudio.Wave;

namespace Recap.Core.Audio;

public static class AutoTrimmer
{
    private const int WindowMs = 20;
    private const int MinSilenceMs = 600;
    private const int PaddingMs = 200;

    public record TrimResult(bool Success, string? OutputPath, string? Error);

    public static TrimResult Trim(string inputPath, string outputPath,
        int minSilenceMs = MinSilenceMs, int paddingMs = PaddingMs)
    {
        using var stream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new WaveFileReader(stream);
        var format = reader.WaveFormat;

        if (format.BitsPerSample != 16 || format.Channels != 1)
            return new TrimResult(false, null, "Unexpected format");

        var samples = ReadSamples(reader);
        if (samples.Length == 0)
            return new TrimResult(false, null, "Empty file");

        int windowSize = format.SampleRate * WindowMs / 1000;
        var rmsWindows = ComputeRms(samples, windowSize);

        if (rmsWindows.Length == 0)
            return new TrimResult(false, null, "File too short");

        float speechThreshold = ComputeSpeechThreshold(rmsWindows);
        if (speechThreshold < 0)
            return new TrimResult(false, null, "Cannot distinguish speech from silence — audio too uniform");

        int minSilenceWindows = minSilenceMs / WindowMs;
        int paddingWindows = paddingMs / WindowMs;

        var isSpeech = new bool[rmsWindows.Length];
        for (int i = 0; i < rmsWindows.Length; i++)
            isSpeech[i] = rmsWindows[i] > speechThreshold;

        var regions = FindSpeechRegions(isSpeech, minSilenceWindows, paddingWindows);

        if (regions.Count == 0)
            return new TrimResult(false, null, "No speech detected");

        using var writer = new WaveFileWriter(outputPath, format);
        foreach (var (start, end) in regions)
        {
            int sampleStart = start * windowSize;
            int sampleEnd = Math.Min(end * windowSize, samples.Length);
            for (int i = sampleStart; i < sampleEnd; i++)
            {
                writer.WriteByte((byte)(samples[i] & 0xFF));
                writer.WriteByte((byte)((samples[i] >> 8) & 0xFF));
            }
        }

        return new TrimResult(true, outputPath, null);
    }

    /// <summary>
    /// Compute speech threshold adaptively.
    /// Uses percentile gap detection: find the natural boundary between
    /// low-energy (silence) and high-energy (speech) windows.
    /// Works for whispers, normal speech, and loud recordings.
    /// </summary>
    private static float ComputeSpeechThreshold(float[] rmsWindows)
    {
        var sorted = rmsWindows.Where(r => r > 1e-7f).OrderBy(r => r).ToArray();
        if (sorted.Length < 4)
            return -1;

        // Find the biggest ratio gap between consecutive sorted values.
        // This naturally separates silence cluster from speech cluster,
        // regardless of absolute amplitude.
        float bestGapRatio = 0;
        int bestGapIdx = -1;
        // Only look in the middle 80% to avoid edge artifacts
        int start = sorted.Length / 10;
        int end = sorted.Length * 9 / 10;
        for (int i = start; i < end; i++)
        {
            if (sorted[i] < 1e-7f) continue;
            float ratio = sorted[i + 1] / sorted[i];
            if (ratio > bestGapRatio)
            {
                bestGapRatio = ratio;
                bestGapIdx = i;
            }
        }

        // Need at least 2x ratio gap to consider it a real boundary
        if (bestGapRatio < 2.0f || bestGapIdx < 0)
            return -1;

        // Threshold = geometric mean of values at the gap boundary
        float threshold = MathF.Sqrt(sorted[bestGapIdx] * sorted[bestGapIdx + 1]);
        return threshold;
    }

    /// <summary>
    /// Returns a mask array where 1 = silence (would be removed), 0 = speech (would be kept).
    /// </summary>
    public static float[]? GetSilenceMask(string filePath, int width,
        int minSilenceMs = MinSilenceMs, int paddingMs = PaddingMs)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new WaveFileReader(stream);
            var format = reader.WaveFormat;
            if (format.BitsPerSample != 16 || format.Channels != 1)
                return null;

            var samples = ReadSamples(reader);
            if (samples.Length == 0) return null;

            int windowSize = format.SampleRate * WindowMs / 1000;
            var rmsWindows = ComputeRms(samples, windowSize);
            if (rmsWindows.Length == 0) return null;

            float speechThreshold = ComputeSpeechThreshold(rmsWindows);
            if (speechThreshold < 0) return null;

            int minSilenceWindows = minSilenceMs / WindowMs;
            int paddingWindows = paddingMs / WindowMs;

            var isSpeech = new bool[rmsWindows.Length];
            for (int i = 0; i < rmsWindows.Length; i++)
                isSpeech[i] = rmsWindows[i] > speechThreshold;

            var regions = FindSpeechRegions(isSpeech, minSilenceWindows, paddingWindows);

            var mask = new float[width];
            for (int i = 0; i < width; i++)
            {
                int windowIdx = (int)((double)i / width * rmsWindows.Length);
                windowIdx = Math.Clamp(windowIdx, 0, rmsWindows.Length - 1);

                bool inSpeechRegion = false;
                foreach (var (start, end) in regions)
                {
                    if (windowIdx >= start && windowIdx < end)
                    {
                        inSpeechRegion = true;
                        break;
                    }
                }
                mask[i] = inSpeechRegion ? 0f : 1f;
            }

            return mask;
        }
        catch
        {
            return null;
        }
    }

    private static short[] ReadSamples(WaveFileReader reader)
    {
        var bytes = new byte[reader.Length];
        int read = reader.Read(bytes, 0, bytes.Length);
        var samples = new short[read / 2];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (short)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
        return samples;
    }

    private static float[] ComputeRms(short[] samples, int windowSize)
    {
        int count = samples.Length / windowSize;
        var rms = new float[count];
        for (int w = 0; w < count; w++)
        {
            double sum = 0;
            for (int i = 0; i < windowSize; i++)
            {
                float s = samples[w * windowSize + i] / 32768f;
                sum += s * s;
            }
            rms[w] = MathF.Sqrt((float)(sum / windowSize));
        }
        return rms;
    }

    private static List<(int Start, int End)> FindSpeechRegions(
        bool[] isSpeech, int minSilenceWindows, int paddingWindows)
    {
        var regions = new List<(int Start, int End)>();
        int? regionStart = null;
        int silenceCount = 0;

        for (int i = 0; i < isSpeech.Length; i++)
        {
            if (isSpeech[i])
            {
                if (regionStart == null)
                    regionStart = Math.Max(0, i - paddingWindows);
                silenceCount = 0;
            }
            else if (regionStart != null)
            {
                silenceCount++;
                if (silenceCount >= minSilenceWindows)
                {
                    int end = Math.Min(i - silenceCount + paddingWindows + 1, isSpeech.Length);
                    regions.Add((regionStart.Value, end));
                    regionStart = null;
                    silenceCount = 0;
                }
            }
        }

        if (regionStart != null)
            regions.Add((regionStart.Value, isSpeech.Length));

        return regions;
    }

    // Testable helpers
    public static float[] ComputeRmsPublic(short[] samples, int windowSize) => ComputeRms(samples, windowSize);
}
