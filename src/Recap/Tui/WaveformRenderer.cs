namespace Recap.Tui;

public static class WaveformRenderer
{
    private static readonly char[] Bars = { ' ', '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };

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

    public static string RenderSegment(string filePath, int width)
    {
        // Read WAV and generate sparkline — open with FileShare.ReadWrite
        // so we never block recording or other readers
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

            return Render(peaks, width);
        }
        catch
        {
            return new string('?', width);
        }
    }
}
