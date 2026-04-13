using Recap.Core.Config;

namespace Recap.Core.Logging;

public static class Log
{
    private static readonly string LogPath = Path.Combine(AppConfig.ConfigDir, "recap.log");
    private const long MaxLogSize = 1024 * 1024; // 1MB
    private static readonly object _lock = new();

    public static void Info(string message) => Write("INF", message);
    public static void Warn(string message) => Write("WRN", message);
    public static void Error(string message) => Write("ERR", message);
    public static void Error(string message, Exception ex) => Write("ERR", $"{message}: {ex.Message}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                RotateIfNeeded();
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}\n";
                File.AppendAllText(LogPath, line);
            }
        }
        catch { } // Never crash on logging
    }

    private static void RotateIfNeeded()
    {
        try
        {
            if (!File.Exists(LogPath)) return;
            var info = new FileInfo(LogPath);
            if (info.Length > MaxLogSize)
            {
                var backup = LogPath + ".old";
                if (File.Exists(backup)) File.Delete(backup);
                File.Move(LogPath, backup);
            }
        }
        catch { }
    }
}
