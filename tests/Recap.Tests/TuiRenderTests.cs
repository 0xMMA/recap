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
