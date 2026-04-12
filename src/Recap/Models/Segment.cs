namespace Recap.Models;

public class Segment
{
    public int Index { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public DateTime RecordedAt { get; set; }
    public bool IsDeleted { get; set; }
}
