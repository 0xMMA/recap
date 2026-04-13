using System.Text.Json;
using System.Text.Json.Serialization;

namespace Recap.Core.Config;

public class AppConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "scribe_v2";
    public string DefaultLanguage { get; set; } = "de";
    public bool AudioEvents { get; set; } = true;
    public string TimestampGranularity { get; set; } = "segment";
    public bool PushToTalk { get; set; }
    public bool Diarization { get; set; } // hardcoded false, power-user only
    public int AutoTrimMinSilenceMs { get; set; } = 600;
    public int AutoTrimPaddingMs { get; set; } = 200;
    public float AutoTrimNoiseFloorDb { get; set; } = -30f;

    [JsonIgnore]
    public static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "recap");

    [JsonIgnore]
    public static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return new AppConfig();

        var json = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}
