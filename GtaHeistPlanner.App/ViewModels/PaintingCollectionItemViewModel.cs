using CommunityToolkit.Mvvm.ComponentModel;
using GtaHeistPlanner.Core.Paintings;

namespace GtaHeistPlanner.App.ViewModels;

public partial class PaintingCollectionItemViewModel(PaintingDefinition painting, Action<string, bool> update) : ObservableObject
{
    public string Id => painting.Id;
    public string Name => painting.Name;
    [ObservableProperty] public partial bool IsCollected { get; set; }
    partial void OnIsCollectedChanged(bool value) => update(Id, value);
}
