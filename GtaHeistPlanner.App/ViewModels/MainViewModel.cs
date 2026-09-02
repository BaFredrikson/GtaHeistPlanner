using CommunityToolkit.Mvvm.ComponentModel;

namespace GtaHeistPlanner.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Greeting { get; set; } = "Welcome to Avalonia!";
}
