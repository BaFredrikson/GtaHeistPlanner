using CommunityToolkit.Mvvm.ComponentModel;
using GtaHeistPlanner.Core.Security;

namespace GtaHeistPlanner.App.ViewModels;

public partial class MapInteractionMarkerViewModel(MapInteractionMarker definition) : ViewModelBase
{
    public string Id { get; } = definition.Id;
    public string MapId { get; } = definition.MapId;
    public MapInteractionMarkerType Type { get; } = definition.Type;
    [ObservableProperty] public partial double X { get; set; } = definition.X;
    [ObservableProperty] public partial double Y { get; set; } = definition.Y;
    [ObservableProperty] public partial string? DisplayName { get; set; } = definition.DisplayName;
    public string EffectiveDisplayName => string.IsNullOrWhiteSpace(DisplayName)
        ? MapInteractionMarkerIcons.DefaultDisplayName(Type)
        : DisplayName;
    public MapInteractionMarker ToDomain() => new(Id, MapId, Type, X, Y, DisplayName);
}
