using NAudio.Wave;

namespace Recap.Core.Audio;

public static class AutoTrimmer
{
    private const int WindowMs = 20;
    private const int MinSilenceMs = 600;
    private const int PaddingMs = 200;
    private const float NoiseFloorThresholdDbfs = -30f;

    public record TrimResult(bool Success, string? OutputPath, string? Error);

    public static TrimResult Trim(string inputPath, string outputPath)
    {
        using var reader = new WaveFileReader(inputPath);
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

        // Noise floor from first 300ms (15 windows at 20ms)
        int noiseWindows = Math.Min(15, rmsWindows.Length);
        float noiseFloor = 0;
        for (int i = 0; i < noiseWindows; i++)
            noiseFloor += rmsWindows[i];
        noiseFloor /= noiseWindows;

        // Safety check: bail if noise floor too high
        float noiseFloorDb = 20 * MathF.Log10(Math.Max(noiseFloor, 1e-10f));
        if (noiseFloorDb > NoiseFloorThresholdDbfs)
            return new TrimResult(false, null, $"Noise floor too high ({noiseFloorDb:F1} dBFS). Mic too hot or noisy environment.");

        float speechThreshold = noiseFloor * 4;
        int minSilenceWindows = MinSilenceMs / WindowMs;
        int paddingWindows = PaddingMs / WindowMs;

        // Mark speech/silence
        var isSpeech = new bool[rmsWindows.Length];
        for (int i = 0; i < rmsWindows.Length; i++)
            isSpeech[i] = rmsWindows[i] > speechThreshold;

        // Find speech regions
        var regions = FindSpeechRegions(isSpeech, minSilenceWindows, paddingWindows);

        if (regions.Count == 0)
            return new TrimResult(false, null, "No speech detected");

        // Write output
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

    /// <summary>
    /// Returns a mask array of length <paramref name="width"/> where 1 = silence (would be removed)
    /// and 0 = speech (would be kept). Returns null if the file cannot be analyzed.
    /// </summary>
    public static float[]? GetSilenceMask(string filePath, int width)
    {
        try
        {
            using var reader = new WaveFileReader(filePath);
            var format = reader.WaveFormat;
            if (format.BitsPerSample != 16 || format.Channels != 1)
                return null;

            var samples = ReadSamples(reader);
            if (samples.Length == 0) return null;

            int windowSize = format.SampleRate * WindowMs / 1000;
            var rmsWindows = ComputeRms(samples, windowSize);
            if (rmsWindows.Length == 0) return null;

            int noiseWindows = Math.Min(15, rmsWindows.Length);
            float noiseFloor = 0;
            for (int i = 0; i < noiseWindows; i++)
                noiseFloor += rmsWindows[i];
            noiseFloor /= noiseWindows;

            float noiseFloorDb = 20 * MathF.Log10(Math.Max(noiseFloor, 1e-10f));
            if (noiseFloorDb > NoiseFloorThresholdDbfs)
                return null;

            float speechThreshold = noiseFloor * 4;
            int minSilenceWindows = MinSilenceMs / WindowMs;
            int paddingWindows = PaddingMs / WindowMs;

            var isSpeech = new bool[rmsWindows.Length];
            for (int i = 0; i < rmsWindows.Length; i++)
                isSpeech[i] = rmsWindows[i] > speechThreshold;

            var regions = FindSpeechRegions(isSpeech, minSilenceWindows, paddingWindows);

            // Build mask: 1 = silence/remove, 0 = speech/keep
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

    // Testable helper
    public static float[] ComputeRmsPublic(short[] samples, int windowSize) => ComputeRms(samples, windowSize);
}
