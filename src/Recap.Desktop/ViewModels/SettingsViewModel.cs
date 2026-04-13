using CommunityToolkit.Mvvm.ComponentModel;
using Recap.Core.Config;

namespace Recap.Desktop.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty] private string _apiKey = "";
    [ObservableProperty] private string _defaultLanguage = "de";
    [ObservableProperty] private bool _pushToTalk;
    [ObservableProperty] private bool _audioEvents = true;
    [ObservableProperty] private string _timestampGranularity = "segment";
    [ObservableProperty] private int _autoTrimMinSilenceMs = 600;
    [ObservableProperty] private int _autoTrimPaddingMs = 200;
    [ObservableProperty] private float _autoTrimNoiseFloorDb = -30f;

    public SettingsViewModel() { }

    public SettingsViewModel(AppConfig config)
    {
        ApiKey = config.ApiKey;
        DefaultLanguage = config.DefaultLanguage;
        PushToTalk = config.PushToTalk;
        AudioEvents = config.AudioEvents;
        TimestampGranularity = config.TimestampGranularity;
        AutoTrimMinSilenceMs = config.AutoTrimMinSilenceMs;
        AutoTrimPaddingMs = config.AutoTrimPaddingMs;
        AutoTrimNoiseFloorDb = config.AutoTrimNoiseFloorDb;
    }

    public AppConfig ToConfig()
    {
        var config = AppConfig.Load();
        config.ApiKey = ApiKey;
        config.DefaultLanguage = DefaultLanguage;
        config.PushToTalk = PushToTalk;
        config.AudioEvents = AudioEvents;
        config.TimestampGranularity = TimestampGranularity;
        config.AutoTrimMinSilenceMs = AutoTrimMinSilenceMs;
        config.AutoTrimPaddingMs = AutoTrimPaddingMs;
        config.AutoTrimNoiseFloorDb = AutoTrimNoiseFloorDb;
        return config;
    }
}
