using CommunityToolkit.Mvvm.ComponentModel;
using Recap.Core.Models;

namespace Recap.Desktop.ViewModels;

public partial class SegmentViewModel : ObservableObject
{
    private readonly Segment _segment;

    public SegmentViewModel(Segment segment) => _segment = segment;

    public Segment Model => _segment;
    public int DisplayIndex => _segment.Index + 1;
    public string Duration => _segment.Duration.ToString(@"mm\:ss\.f");
    public string FilePath => _segment.FilePath;
    public bool HasFile => _segment.HasFile;

    public void Refresh() => OnPropertyChanged(string.Empty);
}
