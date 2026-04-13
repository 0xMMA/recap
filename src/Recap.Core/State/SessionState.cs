using Recap.Core.Models;

namespace Recap.Core.State;

public class SessionState
{
    private readonly List<Segment> _segments = new();
    private Segment? _lastDeleted;
    private int _lastDeletedIndex = -1;
    private int _selectedIndex;
    private int _selectionAnchor = -1;

    public string SessionId { get; } = Guid.NewGuid().ToString("N")[..8];
    public IReadOnlyList<Segment> Segments => _segments.AsReadOnly();
    public int SelectedIndex
    {
        get => _selectedIndex;
        set => _selectedIndex = Math.Clamp(value, 0, Math.Max(0, _segments.Count - 1));
    }
    public int SelectionAnchor
    {
        get => _selectionAnchor;
        set => _selectionAnchor = value;
    }
    public bool IsDirty { get; set; }
    public AppMode Mode { get; set; } = AppMode.Normal;
    public RecordingState RecordingState { get; set; } = RecordingState.Idle;
    public string ActiveLanguage { get; set; } = "de";
    public string? LastActionResult { get; set; }
    public string? TranscriptText { get; set; }
    public int ResultScrollOffset { get; set; }

    public (int Start, int End) GetSelectionRange()
    {
        if (_selectionAnchor < 0 || _segments.Count == 0)
            return (_selectedIndex, _selectedIndex);

        var start = Math.Min(_selectionAnchor, _selectedIndex);
        var end = Math.Max(_selectionAnchor, _selectedIndex);
        return (start, Math.Min(end, _segments.Count - 1));
    }

    public void AddSegment(Segment segment)
    {
        segment.Index = _segments.Count;
        segment.RefreshHasFile();
        _segments.Add(segment);
        _selectedIndex = _segments.Count - 1;
        _selectionAnchor = -1;
        IsDirty = true;
    }

    public void DeleteSelected()
    {
        if (_segments.Count == 0) return;

        var (start, end) = GetSelectionRange();
        // Single-level undo: only stores last single deletion
        if (start == end)
        {
            _lastDeleted = _segments[start];
            _lastDeletedIndex = start;
        }
        else
        {
            _lastDeleted = null;
            _lastDeletedIndex = -1;
        }

        _segments.RemoveRange(start, end - start + 1);
        ReIndex();
        _selectedIndex = Math.Min(start, Math.Max(0, _segments.Count - 1));
        _selectionAnchor = -1;
        IsDirty = true;
    }

    public bool Undo()
    {
        if (_lastDeleted == null || _lastDeletedIndex < 0)
            return false;

        var idx = Math.Min(_lastDeletedIndex, _segments.Count);
        _segments.Insert(idx, _lastDeleted);
        ReIndex();
        _selectedIndex = idx;
        _lastDeleted = null;
        _lastDeletedIndex = -1;
        IsDirty = true;
        return true;
    }

    public void MoveSelection(int delta)
    {
        if (_segments.Count == 0) return;
        _selectionAnchor = -1;
        SelectedIndex += delta;
    }

    public void ExtendSelection(int delta)
    {
        if (_segments.Count == 0) return;
        if (_selectionAnchor < 0)
            _selectionAnchor = _selectedIndex;
        SelectedIndex += delta;
    }

    public void SelectAll()
    {
        if (_segments.Count == 0) return;
        _selectionAnchor = 0;
        _selectedIndex = _segments.Count - 1;
    }

    public void Clear()
    {
        _segments.Clear();
        _lastDeleted = null;
        _lastDeletedIndex = -1;
        _selectedIndex = 0;
        _selectionAnchor = -1;
        IsDirty = false;
        TranscriptText = null;
        LastActionResult = null;
        ResultScrollOffset = 0;
    }

    public Segment? GetSelected()
    {
        if (_segments.Count == 0) return null;
        return _segments[_selectedIndex];
    }

    public List<Segment> GetSelectedSegments()
    {
        if (_segments.Count == 0) return new();
        var (start, end) = GetSelectionRange();
        return _segments.GetRange(start, end - start + 1);
    }

    public TimeSpan TotalDuration => _segments.Aggregate(TimeSpan.Zero, (sum, s) => sum + s.Duration);

    private void ReIndex()
    {
        for (int i = 0; i < _segments.Count; i++)
            _segments[i].Index = i;
    }
}
