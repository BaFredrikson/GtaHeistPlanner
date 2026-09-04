using Avalonia.Controls;
using Avalonia.Input;
using GtaHeistPlanner.App.ViewModels;

namespace GtaHeistPlanner.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Escape || DataContext is not MainViewModel { HasFocusedMap: true } viewModel)
            return;
        viewModel.ExitMapFocusCommand.Execute(null);
        e.Handled = true;
    }
}
