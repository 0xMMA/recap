using Avalonia.Controls;
using Avalonia.Input;
using Recap.Core.Models;
using Recap.Desktop.ViewModels;

namespace Recap.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainWindowViewModel();
        DataContext = _vm;
        _vm.RecoverSession();
        Closing += (_, _) => _vm.Cleanup();
        KeyDown += OnKeyDown;
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        switch (e.Key)
        {
            case Key.R when !ctrl:
                _vm.ToggleRecordingCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Up when shift:
                _vm.ExtendSelection(-1);
                e.Handled = true;
                break;
            case Key.Down when shift:
                _vm.ExtendSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                _vm.MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Down:
                _vm.MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Delete:
            case Key.Back:
                _vm.DeleteSelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Z when ctrl:
                _vm.UndoCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.A when ctrl:
                _vm.SelectAllCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Return when shift:
                _vm.PlayAllCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Return:
                _vm.PlaySelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Space when _vm.RecordingState != RecordingState.Recording:
                _vm.TogglePauseCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.X when shift:
                await _vm.TranscribeAllCommand.ExecuteAsync(null);
                e.Handled = true;
                break;
            case Key.X:
                await _vm.TranscribeCommand.ExecuteAsync(null);
                e.Handled = true;
                break;
            case Key.S:
                // Save - will be implemented in Task 5
                e.Handled = true;
                break;
            case Key.L:
                _vm.CycleLanguageCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F:
                _vm.OpenInExplorerCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.N:
                _vm.NewSessionCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Q:
                Close();
                e.Handled = true;
                break;
        }
    }
}
