using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GtaHeistPlanner.Core.Security;

namespace GtaHeistPlanner.App.ViewModels;

public partial class SecurityPatrolViewModel(SecurityPatrolDefinition definition) : ViewModelBase
{
    public string Id { get; } = definition.Id;
    public string MapId { get; } = definition.MapId;
    [ObservableProperty] public partial string Name { get; set; } = definition.Name;
    [ObservableProperty] public partial bool IsActive { get; set; } = true;
    public ObservableCollection<PatrolWaypoint> Waypoints { get; } = new(definition.Waypoints);
    public SecurityPatrolDefinition ToDomain() => new(Id, MapId, Name, Waypoints.ToList());
    public override string ToString() => Name;
}
