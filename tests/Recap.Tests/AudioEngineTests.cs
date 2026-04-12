using NAudio.Wave;
using Recap.Audio;
using Shouldly;

namespace Recap.Tests;

public class AudioEngineTests : IDisposable
{
    private readonly string _tempDir;

    public AudioEngineTests()
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
    public void SpliceSegments_ConcatsPcmData()
    {
        var file1 = CreateTestWav("a.wav", new short[] { 100, 200, 300 });
        var file2 = CreateTestWav("b.wav", new short[] { 400, 500 });
        var output = Path.Combine(_tempDir, "spliced.wav");

        AudioEngine.SpliceSegments(new[] { file1, file2 }, output);

        File.Exists(output).ShouldBeTrue();
        using var reader = new WaveFileReader(output);
        reader.SampleCount.ShouldBe(5);
    }

    [Fact]
    public void SpliceSegments_PreservesOrder()
    {
        var file1 = CreateTestWav("a.wav", new short[] { 1000 });
        var file2 = CreateTestWav("b.wav", new short[] { 2000 });
        var output = Path.Combine(_tempDir, "spliced.wav");

        AudioEngine.SpliceSegments(new[] { file1, file2 }, output);

        using var reader = new WaveFileReader(output);
        var buffer = new byte[4];
        reader.Read(buffer, 0, 4);
        short first = (short)(buffer[0] | (buffer[1] << 8));
        short second = (short)(buffer[2] | (buffer[3] << 8));
        first.ShouldBe((short)1000);
        second.ShouldBe((short)2000);
    }

    [Fact]
    public void SpliceSegments_OutputFormatMatchesCapture()
    {
        var file1 = CreateTestWav("a.wav", new short[] { 100 });
        var output = Path.Combine(_tempDir, "spliced.wav");

        AudioEngine.SpliceSegments(new[] { file1 }, output);

        using var reader = new WaveFileReader(output);
        reader.WaveFormat.SampleRate.ShouldBe(AudioEngine.SampleRate);
        reader.WaveFormat.BitsPerSample.ShouldBe(AudioEngine.BitsPerSample);
        reader.WaveFormat.Channels.ShouldBe(AudioEngine.Channels);
    }
}
