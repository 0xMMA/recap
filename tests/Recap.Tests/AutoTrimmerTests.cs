using NAudio.Wave;
using Recap.Core.Audio;
using Shouldly;

namespace Recap.Tests;

public class AutoTrimmerTests : IDisposable
{
    private readonly string _tempDir;

    public AutoTrimmerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "recap_tests", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string CreateTestWav(string name, short[] samples)
    {
        var path = Path.Combine(_tempDir, name);
        var format = new WaveFormat(AudioEngine.SampleRate, AudioEngine.BitsPerSample, AudioEngine.Channels);
        using var writer = new WaveFileWriter(path, format);
        foreach (var s in samples)
        {
            writer.WriteByte((byte)(s & 0xFF));
            writer.WriteByte((byte)((s >> 8) & 0xFF));
        }
        return path;
    }

    [Fact]
    public void ComputeRms_CorrectWindowCount()
    {
        var samples = new short[1600]; // 100ms at 16kHz
        int windowSize = 320; // 20ms
        var rms = AutoTrimmer.ComputeRmsPublic(samples, windowSize);
        rms.Length.ShouldBe(5);
    }

    [Fact]
    public void ComputeRms_SilenceIsZero()
    {
        var samples = new short[320]; // one window
        var rms = AutoTrimmer.ComputeRmsPublic(samples, 320);
        rms[0].ShouldBe(0f);
    }

    [Fact]
    public void ComputeRms_LoudSignalIsHigh()
    {
        var samples = new short[320];
        Array.Fill(samples, (short)16000);
        var rms = AutoTrimmer.ComputeRmsPublic(samples, 320);
        rms[0].ShouldBeGreaterThan(0.4f);
    }

    [Fact]
    public void Trim_EmptyFile_ReturnsError()
    {
        var path = CreateTestWav("empty.wav", Array.Empty<short>());
        var output = Path.Combine(_tempDir, "out.wav");
        var result = AutoTrimmer.Trim(path, output);
        result.Success.ShouldBeFalse();
    }

    [Fact]
    public void Trim_AllSilence_ReturnsNoSpeech()
    {
        // Create quiet signal (well below threshold)
        var samples = new short[16000 * 2]; // 2 seconds silence
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (short)(Random.Shared.Next(-5, 5)); // tiny noise

        var path = CreateTestWav("silence.wav", samples);
        var output = Path.Combine(_tempDir, "out.wav");
        var result = AutoTrimmer.Trim(path, output);
        result.Success.ShouldBeFalse();
    }

    [Fact]
    public void Trim_LoudSignalThroughout_BailsOnNoiseFloor()
    {
        // Loud noise everywhere → noise floor too high
        var samples = new short[16000 * 2];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (short)(Random.Shared.Next(-20000, 20000));

        var path = CreateTestWav("loud.wav", samples);
        var output = Path.Combine(_tempDir, "out.wav");
        var result = AutoTrimmer.Trim(path, output);
        result.Success.ShouldBeFalse();
        result.Error.ShouldContain("Noise floor too high");
    }
}
