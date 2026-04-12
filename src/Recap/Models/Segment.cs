namespace Recap.Models;

public class Segment
{
    public int Index { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public DateTime RecordedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Cached file existence — avoids File.Exists() syscall every frame
    private bool _hasFileCached;
    private string? _cachedFilePath;

    public bool HasFile
    {
        get
        {
            if (_cachedFilePath != FilePath)
                RefreshHasFile();
            return _hasFileCached;
        }
    }

    public void RefreshHasFile()
    {
        _cachedFilePath = FilePath;
        _hasFileCached = !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath);
    }
}
