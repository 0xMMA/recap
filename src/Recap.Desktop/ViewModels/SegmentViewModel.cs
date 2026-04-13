using CommunityToolkit.Mvvm.ComponentModel;
using Recap.Core.Models;

namespace Recap.Desktop.ViewModels;

public partial class SegmentViewModel : ObservableObject
{
    private readonly Segment _segment;

    public SegmentViewModel(Segment segment) => _segment = segment;

    [ObservableProperty]
    private bool _isPlaying;

    public Segment Model => _segment;
    public int DisplayIndex => _segment.Index + 1;
    public string Duration
    {
        get
        {
            var ts = _segment.Duration;
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1)
                return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
            return $"~{(int)ts.TotalSeconds}s";
        }
    }
    public string FilePath => _segment.FilePath;
    public bool HasFile => _segment.HasFile;

    public void Refresh() => OnPropertyChanged(string.Empty);
}
