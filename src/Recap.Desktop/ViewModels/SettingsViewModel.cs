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

    public SettingsViewModel() { }

    public SettingsViewModel(AppConfig config)
    {
        ApiKey = config.ApiKey;
        DefaultLanguage = config.DefaultLanguage;
        PushToTalk = config.PushToTalk;
        AudioEvents = config.AudioEvents;
        TimestampGranularity = config.TimestampGranularity;
    }

    public AppConfig ToConfig()
    {
        var config = AppConfig.Load();
        config.ApiKey = ApiKey;
        config.DefaultLanguage = DefaultLanguage;
        config.PushToTalk = PushToTalk;
        config.AudioEvents = AudioEvents;
        config.TimestampGranularity = TimestampGranularity;
        return config;
    }
}
