using Recap.Core.Config;
using Recap.Core.Models;
using Recap.Core.State;
using Shouldly;

namespace Recap.Tests;

public class ViewModelTests
{
    [Fact]
    public void SessionState_DefaultValues()
    {
        var state = new SessionState();
        state.Segments.Count.ShouldBe(0);
        state.RecordingState.ShouldBe(RecordingState.Idle);
        state.ActiveLanguage.ShouldBe("de");
        state.IsDirty.ShouldBeFalse();
        state.TranscriptText.ShouldBeNull();
    }

    [Fact]
    public void DeleteSelected_OnEmptyState_DoesNotThrow()
    {
        var state = new SessionState();
        Should.NotThrow(() => state.DeleteSelected());
        state.Segments.Count.ShouldBe(0);
    }

    [Fact]
    public void Undo_OnEmptyState_ReturnsFalse()
    {
        var state = new SessionState();
        state.Undo().ShouldBeFalse();
    }

    [Fact]
    public void CycleLanguage_TogglesCorrectly()
    {
        var state = new SessionState();
        state.ActiveLanguage.ShouldBe("de");

        // Simulate cycle: de -> en -> auto -> de
        state.ActiveLanguage = CycleLanguage(state.ActiveLanguage);
        state.ActiveLanguage.ShouldBe("en");

        state.ActiveLanguage = CycleLanguage(state.ActiveLanguage);
        state.ActiveLanguage.ShouldBe("auto");

        state.ActiveLanguage = CycleLanguage(state.ActiveLanguage);
        state.ActiveLanguage.ShouldBe("de");
    }

    [Fact]
    public void NewSession_ClearsSegments()
    {
        var state = new SessionState();
        state.AddSegment(new Segment
        {
            FilePath = "/tmp/test.wav",
            Duration = TimeSpan.FromSeconds(5),
            RecordedAt = DateTime.Now
        });
        state.Segments.Count.ShouldBe(1);

        state.Clear();
        state.Segments.Count.ShouldBe(0);
        state.TranscriptText.ShouldBeNull();
        state.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void SelectAll_WithSegments_SetsCorrectRange()
    {
        var state = new SessionState();
        for (int i = 0; i < 3; i++)
        {
            state.AddSegment(new Segment
            {
                FilePath = $"/tmp/test{i}.wav",
                Duration = TimeSpan.FromSeconds(1),
                RecordedAt = DateTime.Now
            });
        }

        state.SelectAll();
        var (start, end) = state.GetSelectionRange();
        start.ShouldBe(0);
        end.ShouldBe(2);
    }

    [Fact]
    public void MoveSelection_ClampsToBounds()
    {
        var state = new SessionState();
        state.AddSegment(new Segment
        {
            FilePath = "/tmp/test.wav",
            Duration = TimeSpan.FromSeconds(1),
            RecordedAt = DateTime.Now
        });

        state.MoveSelection(-10);
        state.SelectedIndex.ShouldBe(0);

        state.MoveSelection(10);
        state.SelectedIndex.ShouldBe(0); // only 1 segment
    }

    [Fact]
    public void AppConfig_DefaultLanguage_IsGerman()
    {
        var config = new AppConfig();
        config.DefaultLanguage.ShouldBe("de");
        config.ApiKey.ShouldBe(string.Empty);
    }

    private static string CycleLanguage(string current) => current switch
    {
        "de" => "en",
        "en" => "auto",
        "auto" => "de",
        _ => "de"
    };
}
