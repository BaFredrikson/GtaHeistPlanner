using CommunityToolkit.Mvvm.ComponentModel;
using GtaHeistPlanner.Core.Security;

namespace GtaHeistPlanner.App.ViewModels;

public partial class SecurityCameraViewModel(SecurityCameraDefinition definition) : ViewModelBase
{
    public string Id { get; } = definition.Id;
    public string MapId { get; } = definition.MapId;
    [ObservableProperty] public partial string Name { get; set; } = definition.Name;
    [ObservableProperty] public partial double X { get; set; } = definition.X;
    [ObservableProperty] public partial double Y { get; set; } = definition.Y;
    [ObservableProperty] public partial double RotationDegrees { get; set; } = definition.RotationDegrees;
    [ObservableProperty] public partial double FovDegrees { get; set; } = definition.FovDegrees;
    [ObservableProperty] public partial double Range { get; set; } = definition.Range;
    [ObservableProperty] public partial bool IsActive { get; set; } = true;
    public SecurityCameraDefinition ToDomain() => new(Id, MapId, Name, X, Y, RotationDegrees, FovDegrees, Range);
}
