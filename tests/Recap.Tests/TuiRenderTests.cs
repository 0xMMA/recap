using NAudio.Wave;
using Recap.Audio;
using Recap.State;
using Recap.Tui;
using Shouldly;
using Spectre.Console;
using Spectre.Console.Rendering;
using Spectre.Console.Testing;
using Segment = Recap.Models.Segment;

namespace Recap.Tests;

public class TuiRenderTests : IDisposable
{
    private readonly string _tempDir;

    public TuiRenderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "recap_tests", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        WaveformRenderer.ClearCache();
    }

    public void Dispose()
    {
        WaveformRenderer.ClearCache();
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

    private Segment MakeSegment(string path, TimeSpan? duration = null)
    {
        var seg = new Segment
        {
            FilePath = path,
            Duration = duration ?? TimeSpan.FromSeconds(5),
            RecordedAt = DateTime.Now
        };
        seg.RefreshHasFile();
        return seg;
    }

    // --- SegmentListPanel Tests ---

    [Fact]
    public void SegmentListPanel_EmptyState_ShowsRecordPrompt()
    {
        var state = new SessionState();
        var panel = new SegmentListPanel(state);
        var output = RenderToString(panel);

        output.ShouldContain("No segments");
        output.ShouldContain("R");
    }

    [Fact]
    public void SegmentListPanel_WithSegment_ShowsIndexAndDuration()
    {
        var state = new SessionState();
        var path = CreateTestWav("test.wav", new short[] { 100, 200, 300 });
        state.AddSegment(MakeSegment(path, TimeSpan.FromSeconds(12.5)));

        var panel = new SegmentListPanel(state);
        var output = RenderToString(panel);

        output.ShouldContain("01"); // Index (1-based, zero-padded)
        output.ShouldContain("00:12.5"); // Duration
    }

    [Fact]
    public void SegmentListPanel_SelectedSegment_ShowsIndicator()
    {
        var state = new SessionState();
        var path = CreateTestWav("a.wav", new short[] { 100 });
        state.AddSegment(MakeSegment(path));

        var panel = new SegmentListPanel(state);
        var output = RenderToString(panel);

        output.ShouldContain(">"); // Selection indicator
    }

    [Fact]
    public void SegmentListPanel_MissingFile_ShowsWarning()
    {
        var state = new SessionState();
        var seg = new Segment
        {
            FilePath = Path.Combine(_tempDir, "nonexistent.wav"),
            Duration = TimeSpan.FromSeconds(3),
            RecordedAt = DateTime.Now
        };
        seg.RefreshHasFile();
        state.AddSegment(seg);

        var panel = new SegmentListPanel(state);
        var output = RenderToString(panel);

        output.ShouldContain("⚠");
        output.ShouldContain("·");
    }

    [Fact]
    public void SegmentListPanel_MultipleSegments_ShowsAll()
    {
        var state = new SessionState();
        state.AddSegment(MakeSegment(CreateTestWav("a.wav", new short[] { 100 }), TimeSpan.FromSeconds(2)));
        state.AddSegment(MakeSegment(CreateTestWav("b.wav", new short[] { 200 }), TimeSpan.FromSeconds(4)));
        state.AddSegment(MakeSegment(CreateTestWav("c.wav", new short[] { 300 }), TimeSpan.FromSeconds(6)));

        var panel = new SegmentListPanel(state);
        var output = RenderToString(panel);

        output.ShouldContain("01");
        output.ShouldContain("02");
        output.ShouldContain("03");
    }

    // --- StatusBar Tests ---

    [Fact]
    public void StatusBar_Idle_ShowsIdleState()
    {
        var state = new SessionState();
        var bar = new StatusBar(state, () => true);
        var output = RenderToString(bar);

        output.ShouldContain("IDLE");
    }

    [Fact]
    public void StatusBar_ShowsLanguage()
    {
        var state = new SessionState();
        state.ActiveLanguage = "de";
        var bar = new StatusBar(state, () => true);
        var output = RenderToString(bar);

        output.ShouldContain("DE");
    }

    [Fact]
    public void StatusBar_ShowsSegmentCount()
    {
        var state = new SessionState();
        state.AddSegment(MakeSegment(CreateTestWav("a.wav", new short[] { 1 })));
        state.AddSegment(MakeSegment(CreateTestWav("b.wav", new short[] { 1 })));
        var bar = new StatusBar(state, () => true);
        var output = RenderToString(bar);

        output.ShouldContain("2 seg");
    }

    [Fact]
    public void StatusBar_NoApiKey_ShowsWarning()
    {
        var state = new SessionState();
        var bar = new StatusBar(state, () => false);
        var output = RenderToString(bar);

        output.ShouldContain("API");
        output.ShouldContain("✗");
    }

    [Fact]
    public void StatusBar_WithApiKey_ShowsCheck()
    {
        var state = new SessionState();
        var bar = new StatusBar(state, () => true);
        var output = RenderToString(bar);

        output.ShouldContain("API");
        output.ShouldContain("✓");
    }

    // --- WaveformRenderer Tests ---

    [Fact]
    public void WaveformRenderer_Render_SilenceProducesSpaces()
    {
        var peaks = new float[10];
        var result = WaveformRenderer.Render(peaks, 10);
        result.ShouldBe("          "); // 10 spaces
    }

    [Fact]
    public void WaveformRenderer_Render_FullVolumeProducesFullBars()
    {
        var peaks = Enumerable.Repeat(1.0f, 5).ToArray();
        var result = WaveformRenderer.Render(peaks, 5);
        result.ShouldBe("█████");
    }

    [Fact]
    public void WaveformRenderer_Render_MixedLevels()
    {
        var peaks = new float[] { 0f, 0.5f, 1.0f };
        var result = WaveformRenderer.Render(peaks, 3);
        result.Length.ShouldBe(3);
        result[0].ShouldBe(' ');    // 0.0 = space
        result[2].ShouldBe('█');    // 1.0 = full bar
    }

    [Fact]
    public void WaveformRenderer_Cache_ReturnsSameOnSecondCall()
    {
        var path = CreateTestWav("cached.wav", new short[] { 1000, 2000, 3000, 4000 });

        var first = WaveformRenderer.GetOrRenderSegment(path, 10);
        var second = WaveformRenderer.GetOrRenderSegment(path, 10);

        second.ShouldBe(first);
    }

    [Fact]
    public void WaveformRenderer_Cache_InvalidatesOnExplicitCall()
    {
        var path = CreateTestWav("invalidate.wav", new short[] { 1000, 2000 });

        var first = WaveformRenderer.GetOrRenderSegment(path, 10);
        WaveformRenderer.InvalidateCache(path);
        // Re-render after invalidation — should still produce same result for same file
        var second = WaveformRenderer.GetOrRenderSegment(path, 10);

        second.ShouldBe(first); // Same file, same result
    }

    [Fact]
    public void WaveformRenderer_Cache_DifferentWidthRecomputes()
    {
        var path = CreateTestWav("width.wav", new short[] { 1000, 2000, 3000 });

        var narrow = WaveformRenderer.GetOrRenderSegment(path, 5);
        var wide = WaveformRenderer.GetOrRenderSegment(path, 20);

        narrow.Length.ShouldBe(5);
        wide.Length.ShouldBe(20);
    }

    [Fact]
    public void WaveformRenderer_QuietAudio_NormalizesToVisibleBars()
    {
        // Very quiet signal — only ~3% of max amplitude
        // Without normalization these would be invisible (space chars)
        var samples = new short[1600]; // 100ms at 16kHz
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (short)(Math.Sin(i * 0.1) * 1000); // ~3% amplitude

        var path = CreateTestWav("quiet.wav", samples);
        var sparkline = WaveformRenderer.GetOrRenderSegment(path, 10);

        sparkline.Length.ShouldBe(10);
        // After normalization, should have visible bars (not spaces)
        var barChars = new[] { '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };
        sparkline.Count(c => barChars.Contains(c)).ShouldBeGreaterThan(0,
            "Quiet audio should produce visible bars after normalization");
        sparkline.Trim().ShouldNotBeEmpty();
    }

    [Fact]
    public void WaveformRenderer_LoudAudio_AlsoNormalized()
    {
        // Loud signal at full amplitude — should show visible bars
        var samples = new short[1600];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (short)(Math.Sin(i * 0.1) * 30000); // ~91% amplitude

        var path = CreateTestWav("loud.wav", samples);
        var sparkline = WaveformRenderer.GetOrRenderSegment(path, 10);

        var barChars = new[] { '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };
        sparkline.Count(c => barChars.Contains(c)).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void WaveformRenderer_VaryingLevels_ShowsShape()
    {
        // First half quiet, second half loud — should show contrast
        var samples = new short[3200];
        for (int i = 0; i < 1600; i++)
            samples[i] = (short)(Math.Sin(i * 0.1) * 500);  // quiet
        for (int i = 1600; i < 3200; i++)
            samples[i] = (short)(Math.Sin(i * 0.1) * 10000); // loud

        var path = CreateTestWav("varying.wav", samples);
        var sparkline = WaveformRenderer.GetOrRenderSegment(path, 10);

        // Loud half should have bigger bars than quiet half
        var quietPart = sparkline[..5];
        var loudPart = sparkline[5..];

        // Compute average bar height for each half
        var barValues = new Dictionary<char, int>
        {
            {' ', 0}, {'▁', 1}, {'▂', 2}, {'▃', 3}, {'▄', 4},
            {'▅', 5}, {'▆', 6}, {'▇', 7}, {'█', 8}
        };
        var quietAvg = quietPart.Average(c => (double)barValues.GetValueOrDefault(c, 0));
        var loudAvg = loudPart.Average(c => (double)barValues.GetValueOrDefault(c, 0));

        loudAvg.ShouldBeGreaterThan(quietAvg, "Loud half should have taller bars than quiet half");
    }

    [Fact]
    public void WaveformRenderer_SpeechPattern_ShowsMeaningfulShape()
    {
        // Simulate real speech: bursts of audio with silence gaps
        // ~60% silence, ~40% speech — typical for spoken recap
        var samples = new short[16000]; // 1 second at 16kHz
        for (int i = 0; i < samples.Length; i++)
        {
            // Create 4 "words" with silence between them
            bool isSpeech = (i % 4000) < 1600; // 40% duty cycle
            samples[i] = isSpeech
                ? (short)(Math.Sin(i * 0.3) * 8000) // ~24% amplitude speech
                : (short)(Random.Shared.Next(-30, 30)); // near-silence noise floor
        }

        var path = CreateTestWav("speech.wav", samples);
        var sparkline = WaveformRenderer.GetOrRenderSegment(path, 20);

        sparkline.Length.ShouldBe(20);
        // Should have visible bars (speech portions) and quiet areas
        var barChars = new[] { '▃', '▄', '▅', '▆', '▇', '█' };
        int tallBars = sparkline.Count(c => barChars.Contains(c));
        tallBars.ShouldBeGreaterThan(0, "Speech portions should produce visible bars");
        // Should also have some low/empty columns for silence gaps
        int lowOrEmpty = sparkline.Count(c => c == ' ' || c == '▁');
        lowOrEmpty.ShouldBeGreaterThan(0, "Silence gaps should produce low bars");
    }

    [Fact]
    public void WaveformRenderer_SpeechWithLoudPop_PopDoesntFlattenSpeech()
    {
        // Speech at moderate level + one loud click
        var samples = new short[16000];
        for (int i = 0; i < samples.Length; i++)
        {
            bool isSpeech = (i % 4000) < 1600;
            samples[i] = isSpeech
                ? (short)(Math.Sin(i * 0.3) * 5000) // moderate speech
                : (short)0;
        }
        // Add one loud pop at sample 8000
        for (int i = 8000; i < 8050; i++)
            samples[i] = short.MaxValue;

        var path = CreateTestWav("pop.wav", samples);
        var sparkline = WaveformRenderer.GetOrRenderSegment(path, 20);

        // Speech columns should still have meaningful bars (not squashed by pop)
        // Count columns with mid-range bars (▃▄▅▆▇█)
        var midBars = new[] { '▃', '▄', '▅', '▆', '▇', '█' };
        int visibleCount = sparkline.Count(c => midBars.Contains(c));
        visibleCount.ShouldBeGreaterThan(2,
            "Speech portions should have visible mid-range bars even with a loud pop present");
    }

    [Fact]
    public void SegmentListPanel_WithValidSegment_ShowsSparklineChars()
    {
        var state = new SessionState();
        // Create WAV with actual audio content
        var samples = new short[1600];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (short)(Math.Sin(i * 0.1) * 5000);
        var path = CreateTestWav("sparkline.wav", samples);
        state.AddSegment(MakeSegment(path, TimeSpan.FromSeconds(1)));

        var panel = new SegmentListPanel(state);
        var output = RenderToString(panel);

        // Output should contain at least one waveform bar character (not just spaces)
        var barChars = new[] { '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };
        barChars.ShouldContain(c => output.Contains(c),
            "Segment list should contain visible sparkline bar characters");
    }

    // --- Segment.HasFile Cache Tests ---

    [Fact]
    public void Segment_HasFile_CachesResult()
    {
        var path = CreateTestWav("exists.wav", new short[] { 100 });
        var seg = MakeSegment(path);

        seg.HasFile.ShouldBeTrue();

        // Delete file — cached value should remain true until refreshed
        File.Delete(path);
        seg.HasFile.ShouldBeTrue(); // Still cached

        seg.RefreshHasFile();
        seg.HasFile.ShouldBeFalse(); // Now reflects reality
    }

    [Fact]
    public void Segment_HasFile_InvalidatesOnPathChange()
    {
        var path = CreateTestWav("original.wav", new short[] { 100 });
        var seg = MakeSegment(path);
        seg.HasFile.ShouldBeTrue();

        seg.FilePath = Path.Combine(_tempDir, "nope.wav");
        seg.HasFile.ShouldBeFalse(); // Path changed, cache auto-invalidates
    }

    // --- Helper ---

    private static string RenderToString(IRenderable renderable)
    {
        var console = new TestConsole();
        console.Write(renderable);
        return console.Output;
    }
}
