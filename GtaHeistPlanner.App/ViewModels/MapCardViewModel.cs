using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GtaHeistPlanner.App.Models;
using GtaHeistPlanner.Core.Maps;
using GtaHeistPlanner.Core.Planning;

namespace GtaHeistPlanner.App.ViewModels;

public partial class MapCardViewModel : ViewModelBase, IDisposable
{
    public MapCardViewModel(MainViewModel owner, MapDefinition map)
    {
        Owner = owner;
        Map = map;
        Owner.PropertyChanged += OnOwnerPropertyChanged;
    }

    public MainViewModel Owner { get; }
    public MapDefinition Map { get; }
    public MapViewportState Viewport { get; } = new();
    public string AssetUri => $"avares://GtaHeistPlanner.App/{Map.SvgAssetPath}";
    public bool IsSelected => Owner.SelectedMap.Id == Map.Id;
    public bool ShowLoot => Owner.CurrentPolicy.AllowsOverlay(OverlayType.Loot, Map.Id);
    public bool ShowUnknownLootClearly => Owner.CurrentPolicy.LootVisibility == LootVisibilityMode.AllClearly;
    public bool IsLootEditMode => Owner.DeveloperMode && Owner.IsLootEditMode && ShowLoot;
    public bool ShowGuards => Owner.CurrentPolicy.AllowsOverlay(
        Map.Category == MapCategory.Exterior ? OverlayType.ExteriorGuards : OverlayType.InteriorGuards, Map.Id);
    public bool ShowCameras => Owner.CurrentPolicy.AllowsOverlay(
        Map.Category == MapCategory.Exterior ? OverlayType.ExteriorCameras : OverlayType.InteriorCameras, Map.Id);
    public bool IsSecurityEditMode => Owner.DeveloperMode &&
        Owner.CurrentStage is PlannerStage.HeistInfiltration or PlannerStage.HeistActivity;
    public bool IsSewer => Map.Id == "sewer";
    public bool IsFocused => Owner.FocusedMapId == Map.Id;
    public string FocusGlyph => IsFocused ? "▣" : "⛶";

    [RelayCommand] private void SelectMap() => Owner.SelectedMap = Map;
    [RelayCommand]
    private void FocusMap()
    {
        if (IsFocused) Owner.ExitMapFocusCommand.Execute(null);
        else Owner.FocusMapCommand.Execute(Map.Id);
    }
    [RelayCommand] private void ZoomIn() => Viewport.SetZoom(Viewport.Zoom * 1.25);
    [RelayCommand] private void ZoomOut() => Viewport.SetZoom(Viewport.Zoom / 1.25);
    [RelayCommand] private void ResetViewport() => Viewport.Reset();

    [RelayCommand]
    private void CreateLoot(MapPoint point)
    {
        Owner.SelectedMap = Map;
        Owner.CreateLootCommand.Execute(point);
    }

    [RelayCommand]
    private void MoveLoot(LootMarkerMove move)
    {
        Owner.SelectedMap = Map;
        Owner.MoveLootCommand.Execute(move);
    }

    [RelayCommand] private void SelectLoot(string id) => Owner.SelectLootCommand.Execute(id);

    [RelayCommand]
    private void PlaceSecurityObject(MapPoint point)
    {
        Owner.SelectedMap = Map;
        Owner.PlaceSecurityObjectCommand.Execute(point);
    }

    [RelayCommand] private void MoveSecurityObject(SecurityMarkerMove move) => Owner.MoveSecurityObjectCommand.Execute(move);
    [RelayCommand] private void SelectSecurityCamera(string id) { Owner.SelectedMap = Map; Owner.SelectSecurityCameraCommand.Execute(id); }
    [RelayCommand] private void SelectSecurityGuard(string id) { Owner.SelectedMap = Map; Owner.SelectSecurityGuardCommand.Execute(id); }
    [RelayCommand] private void SelectPatrolWaypoint(SecurityWaypointSelection selection) { Owner.SelectedMap = Map; Owner.SelectPatrolWaypointCommand.Execute(selection); }
    [RelayCommand] private void MovePatrolWaypoint(SecurityWaypointMove move) => Owner.MovePatrolWaypointCommand.Execute(move);

    private void OnOwnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsSelected));
        OnPropertyChanged(nameof(ShowLoot));
        OnPropertyChanged(nameof(ShowUnknownLootClearly));
        OnPropertyChanged(nameof(IsLootEditMode));
        OnPropertyChanged(nameof(ShowGuards));
        OnPropertyChanged(nameof(ShowCameras));
        OnPropertyChanged(nameof(IsSecurityEditMode));
        OnPropertyChanged(nameof(IsFocused));
        OnPropertyChanged(nameof(FocusGlyph));
    }

    public void Dispose() => Owner.PropertyChanged -= OnOwnerPropertyChanged;
}
