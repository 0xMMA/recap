using System.Text.Json;
using NAudio.Wave;
using Recap.Models;

namespace Recap.State;

public static class SessionPersistence
{
    private static string RecapTempDir => Path.Combine(Path.GetTempPath(), "recap");

    public static string GetSessionDir(string sessionId) =>
        Path.Combine(RecapTempDir, sessionId);

    public static void SaveSessionManifest(SessionState state)
    {
        var dir = GetSessionDir(state.SessionId);
        Directory.CreateDirectory(dir);

        var manifest = new SessionManifest
        {
            SessionId = state.SessionId,
            CreatedAt = DateTime.Now,
            Segments = state.Segments.Select(s => new SegmentInfo
            {
                Index = s.Index,
                FilePath = s.FilePath,
                DurationMs = (long)s.Duration.TotalMilliseconds,
                RecordedAt = s.RecordedAt
            }).ToList()
        };

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(dir, "manifest.json"), json);
    }

    public static SessionState? TryRecover()
    {
        if (!Directory.Exists(RecapTempDir)) return null;

        var sessions = Directory.GetDirectories(RecapTempDir)
            .Select(d => new { Dir = d, Manifest = Path.Combine(d, "manifest.json") })
            .Where(x => File.Exists(x.Manifest))
            .OrderByDescending(x => File.GetLastWriteTime(x.Manifest))
            .ToList();

        if (sessions.Count == 0) return null;

        // Try latest session
        var latest = sessions[0];
        try
        {
            var json = File.ReadAllText(latest.Manifest);
            var manifest = JsonSerializer.Deserialize<SessionManifest>(json);
            if (manifest == null || manifest.Segments.Count == 0) return null;

            var state = new SessionState();
            foreach (var segInfo in manifest.Segments)
            {
                if (!File.Exists(segInfo.FilePath)) continue;

                using var reader = new WaveFileReader(segInfo.FilePath);
                state.AddSegment(new Segment
                {
                    FilePath = segInfo.FilePath,
                    Duration = reader.TotalTime,
                    RecordedAt = segInfo.RecordedAt
                });
            }

            if (state.Segments.Count > 0)
            {
                state.IsDirty = true;
                return state;
            }
        }
        catch { }

        return null;
    }

    public static void PruneOldSessions(int keepCount = 3)
    {
        if (!Directory.Exists(RecapTempDir)) return;

        var sessions = Directory.GetDirectories(RecapTempDir)
            .OrderByDescending(d => Directory.GetLastWriteTime(d))
            .Skip(keepCount)
            .ToList();

        foreach (var dir in sessions)
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private class SessionManifest
    {
        public string SessionId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<SegmentInfo> Segments { get; set; } = new();
    }

    private class SegmentInfo
    {
        public int Index { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public long DurationMs { get; set; }
        public DateTime RecordedAt { get; set; }
    }
}
