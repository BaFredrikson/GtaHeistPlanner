using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using GtaHeistPlanner.App.ViewModels;

namespace GtaHeistPlanner.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closed += (_, _) => (DataContext as IDisposable)?.Dispose();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Escape || DataContext is not MainViewModel { HasFocusedMap: true } viewModel)
            return;
        viewModel.ExitMapFocusCommand.Execute(null);
        e.Handled = true;
    }

    private async void LoadHeistClicked(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Kortz heist",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("GTA Heist Planner save") { Patterns = ["*.json"] }],
        });
        if (files.Count == 1 && DataContext is MainViewModel viewModel)
            viewModel.LoadHeistCommand.Execute(files[0].Path.LocalPath);
    }
}
