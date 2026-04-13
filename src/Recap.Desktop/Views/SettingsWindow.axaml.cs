using Avalonia.Controls;
using Avalonia.Interactivity;
using Recap.Core.Updates;
using Recap.Desktop.ViewModels;

namespace Recap.Desktop.Views;

public partial class SettingsWindow : Window
{
    public SettingsViewModel ViewModel { get; }

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        ViewModel = vm;
        DataContext = vm;
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private async void CheckUpdates_Click(object? sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button != null) button.IsEnabled = false;

        try
        {
            var updater = new AppUpdater();
            updater.SetAppAssembly(typeof(MainWindow).Assembly);
            var (available, version) = await updater.CheckForUpdateAsync();

            if (available)
            {
                if (button != null) button.Content = $"Update available: v{version}";
            }
            else
            {
                if (button != null) button.Content = $"Up to date (v{updater.CurrentVersion})";
            }
        }
        catch
        {
            if (button != null) button.Content = "Update check failed";
        }
        finally
        {
            if (button != null) button.IsEnabled = true;
        }
    }
}
