using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GtaHeistPlanner.Core.Security;

namespace GtaHeistPlanner.App.ViewModels;

public partial class SecurityGuardViewModel(SecurityGuardDefinition definition) : ViewModelBase
{
    public string Id { get; } = definition.Id;
    public string MapId { get; } = definition.MapId;
    [ObservableProperty] public partial string Name { get; set; } = definition.Name;
    [ObservableProperty] public partial double X { get; set; } = definition.X;
    [ObservableProperty] public partial double Y { get; set; } = definition.Y;
    [ObservableProperty] public partial bool IsActive { get; set; } = true;
    public ObservableCollection<string> PatrolIds { get; } = new(definition.PatrolIds);
    public IReadOnlyList<string> VoiceAliases { get; } = definition.VoiceAliases;
    public SecurityGuardDefinition ToDomain() => new(Id, MapId, Name, X, Y, PatrolIds.ToList()) { VoiceAliases = VoiceAliases };
}
