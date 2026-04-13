using Velopack;
using Velopack.Sources;

namespace Recap.Core.Updates;

public class AppUpdater
{
    private const string RepoUrl = "https://github.com/0xMMA/recap";
    private UpdateManager? _manager;

    private System.Reflection.Assembly? _appAssembly;

    public string CurrentVersion =>
        (_appAssembly ?? typeof(AppUpdater).Assembly).GetName().Version?.ToString(3) ?? "dev";

    /// Set the main app assembly so version reads from Desktop, not Core
    public void SetAppAssembly(System.Reflection.Assembly assembly) => _appAssembly = assembly;

    private UpdateManager GetManager()
    {
        _manager ??= new UpdateManager(new GithubSource(RepoUrl, null, false));
        return _manager;
    }

    public async Task<(bool Available, string? Version)> CheckForUpdateAsync()
    {
        try
        {
            var mgr = GetManager();
            var info = await mgr.CheckForUpdatesAsync();
            if (info != null)
                return (true, info.TargetFullRelease.Version.ToString());
            return (false, null);
        }
        catch
        {
            return (false, null);
        }
    }

    public async Task<bool> DownloadAndApplyAsync(Action<int>? onProgress = null)
    {
        try
        {
            var mgr = GetManager();
            var info = await mgr.CheckForUpdatesAsync();
            if (info == null) return false;

            await mgr.DownloadUpdatesAsync(info, onProgress);
            mgr.ApplyUpdatesAndRestart(info);
            return true; // won't reach here — app restarts
        }
        catch
        {
            return false;
        }
    }
}
