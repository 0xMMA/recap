using Recap.Models;
using Recap.State;
using Shouldly;

namespace Recap.Tests;

public class SessionStateTests
{
    private static Segment MakeSegment(string path = "test.wav") =>
        new() { FilePath = path, Duration = TimeSpan.FromSeconds(5), RecordedAt = DateTime.Now };

    [Fact]
    public void AddSegment_IncrementsCount()
    {
        var state = new SessionState();
        state.AddSegment(MakeSegment());
        state.Segments.Count.ShouldBe(1);
    }

    [Fact]
    public void AddSegment_SetsIndexSequentially()
    {
        var state = new SessionState();
        state.AddSegment(MakeSegment("a.wav"));
        state.AddSegment(MakeSegment("b.wav"));
        state.Segments[0].Index.ShouldBe(0);
        state.Segments[1].Index.ShouldBe(1);
    }

    [Fact]
    public void AddSegment_SelectsNewest()
    {
        var state = new SessionState();
        state.AddSegment(MakeSegment("a.wav"));
        state.AddSegment(MakeSegment("b.wav"));
        state.SelectedIndex.ShouldBe(1);
    }

    [Fact]
    public void AddSegment_SetsDirty()
    {
        var state = new SessionState();
        state.IsDirty.ShouldBeFalse();
        state.AddSegment(MakeSegment());
        state.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void DeleteSelected_RemovesSegment()
    {
        var state = new SessionState();
        state.AddSegment(MakeSegment("a.wav"));
        state.AddSegment(MakeSegment("b.wav"));
        state.SelectedIndex = 0;
        state.DeleteSelected();
        state.Segments.Count.ShouldBe(1);
        state.Segments[0].FilePath.ShouldBe("b.wav");
    }

    [Fact]
    public void DeleteSelected_ReIndexes()
    {
        var state = new SessionState();
        state.AddSegment(MakeSegment("a.wav"));
        state.AddSegment(MakeSegment("b.wav"));
        state.AddSegment(MakeSegment("c.wav"));
        state.SelectedIndex = 1;
        state.DeleteSelected();
        state.Segments[0].Index.ShouldBe(0);
        state.Segments[1].Index.ShouldBe(1);
    }

    [Fact]
    public void Undo_RestoresDeletedSegment()
    {
        var state = new SessionState();
        state.AddSegment(MakeSegment("a.wav"));
        state.AddSegment(MakeSegment("b.wav"));
        state.SelectedIndex = 0;
        state.DeleteSelected();
        state.Undo().ShouldBeTrue();
        state.Segments.Count.ShouldBe(2);
        state.Segments[0].FilePath.ShouldBe("a.wav");
    }

    [Fact]
    public void Undo_RestoresAtCorrectPosition()
    {
        var state = new SessionState();
        state.AddSegment(MakeSegment("a.wav"));
        state.AddSegment(MakeSegment("b.wav"));
        state.AddSegment(MakeSegment("c.wav"));
        state.SelectedIndex = 1;
        state.DeleteSelected();
        state.Undo();
        state.Segments[1].FilePath.ShouldBe("b.wav");
        state.SelectedIndex.ShouldBe(1);
    }

    [Fact]
    public void Undo_ReturnsFalseWhenNothingToUndo()
    {
        var state = new SessionState();
        state.Undo().ShouldBeFalse();
    }

    [Fact]
    public void Undo_OnlySingleLevel()
    {
        var state = new SessionState();
        state.AddSegment(MakeSegment("a.wav"));
        state.AddSegment(MakeSegment("b.wav"));
        state.SelectedIndex = 0;
        state.DeleteSelected();
        state.SelectedIndex = 0;
        state.DeleteSelected();
        state.Undo().ShouldBeTrue();
        state.Segments.Count.ShouldBe(1);
        // First deletion not recoverable
        state.Undo().ShouldBeFalse();
    }

    [Fact]
    public void MoveSelection_ClampsToBounds()
    {
        var state = new SessionState();
        state.AddSegment(MakeSegment());
        state.AddSegment(MakeSegment());
        state.MoveSelection(-10);
        state.SelectedIndex.ShouldBe(0);
        state.MoveSelection(10);
        state.SelectedIndex.ShouldBe(1);
    }

    [Fact]
    public void ExtendSelection_SetsAnchor()
    {
        var state = new SessionState();
        state.AddSegment(MakeSegment());
        state.AddSegment(MakeSegment());
        state.AddSegment(MakeSegment());
        state.SelectedIndex = 0;
        state.ExtendSelection(1);
        state.ExtendSelection(1);
        var (start, end) = state.GetSelectionRange();
        start.ShouldBe(0);
        end.ShouldBe(2);
    }

    [Fact]
    public void GetSelectedSegments_ReturnsRange()
    {
        var state = new SessionState();
        state.AddSegment(MakeSegment("a.wav"));
        state.AddSegment(MakeSegment("b.wav"));
        state.AddSegment(MakeSegment("c.wav"));
        state.SelectedIndex = 0;
        state.ExtendSelection(1);
        var selected = state.GetSelectedSegments();
        selected.Count.ShouldBe(2);
        selected[0].FilePath.ShouldBe("a.wav");
        selected[1].FilePath.ShouldBe("b.wav");
    }

    [Fact]
    public void Clear_ResetsEverything()
    {
        var state = new SessionState();
        state.AddSegment(MakeSegment());
        state.Clear();
        state.Segments.Count.ShouldBe(0);
        state.IsDirty.ShouldBeFalse();
        state.SelectedIndex.ShouldBe(0);
    }

    [Fact]
    public void TotalDuration_SumsAllSegments()
    {
        var state = new SessionState();
        state.AddSegment(new Segment { FilePath = "a.wav", Duration = TimeSpan.FromSeconds(3) });
        state.AddSegment(new Segment { FilePath = "b.wav", Duration = TimeSpan.FromSeconds(7) });
        state.TotalDuration.ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void DeleteSelected_NoSegments_DoesNotThrow()
    {
        var state = new SessionState();
        Should.NotThrow(() => state.DeleteSelected());
    }

    [Fact]
    public void BulkDelete_RemovesRange()
    {
        var state = new SessionState();
        state.AddSegment(MakeSegment("a.wav"));
        state.AddSegment(MakeSegment("b.wav"));
        state.AddSegment(MakeSegment("c.wav"));
        state.AddSegment(MakeSegment("d.wav"));
        state.SelectedIndex = 1;
        state.ExtendSelection(1); // select 1-2
        state.DeleteSelected();
        state.Segments.Count.ShouldBe(2);
        state.Segments[0].FilePath.ShouldBe("a.wav");
        state.Segments[1].FilePath.ShouldBe("d.wav");
    }

    [Fact]
    public void BulkDelete_NoUndoForMulti()
    {
        var state = new SessionState();
        state.AddSegment(MakeSegment("a.wav"));
        state.AddSegment(MakeSegment("b.wav"));
        state.AddSegment(MakeSegment("c.wav"));
        state.SelectedIndex = 0;
        state.ExtendSelection(1);
        state.DeleteSelected();
        state.Undo().ShouldBeFalse();
    }
}
