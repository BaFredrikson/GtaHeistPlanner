using CommunityToolkit.Mvvm.ComponentModel;
using GtaHeistPlanner.Core.Sewer;

namespace GtaHeistPlanner.App.ViewModels;

public partial class SewerNodeViewModel(SewerNode node) : ViewModelBase
{
    public string Id { get; } = node.Id;
    [ObservableProperty] public partial double MapX { get; set; } = node.MapX;
    [ObservableProperty] public partial double MapY { get; set; } = node.MapY;
    public SewerNode ToDomain() => new(Id, MapX, MapY);
    public override string ToString() => Id;
}
