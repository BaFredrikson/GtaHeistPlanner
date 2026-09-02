using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GtaHeistPlanner.App.Models;
using GtaHeistPlanner.App.Services;
using GtaHeistPlanner.Core.Maps;

namespace GtaHeistPlanner.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public const string ExteriorFirstFloorMapId = "exterior-firstfloor";
    private readonly MapCalibrationStore _calibrationStore = new();
    private readonly MapCalibration _initialCalibration;
    private bool _updatingCalibrationFields;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedMapAssetUri))]
    [NotifyPropertyChangedFor(nameof(IsSecurityOverlayVisible))]
    public partial MapDefinition SelectedMap { get; set; } = KortzMapCatalog.DefaultMap;

    [ObservableProperty]
    public partial MapCalibration CurrentCalibration { get; private set; }

    [ObservableProperty] public partial double ScaleX { get; set; }
    [ObservableProperty] public partial double ScaleY { get; set; }
    [ObservableProperty] public partial double OffsetX { get; set; }
    [ObservableProperty] public partial double OffsetY { get; set; }
    [ObservableProperty] public partial double RotationDegrees { get; set; }
    [ObservableProperty] public partial bool FlipX { get; set; }
    [ObservableProperty] public partial bool FlipY { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHoveredMarker))]
    public partial string? HoveredMarker { get; set; }
    [ObservableProperty] public partial string? SaveStatus { get; set; }

    public IReadOnlyList<MapDefinition> Maps { get; } = KortzMapCatalog.Maps;
    public string SelectedMapAssetUri => $"avares://GtaHeistPlanner.App/{SelectedMap.SvgAssetPath}";
    public bool IsSecurityOverlayVisible => SelectedMap.Id == ExteriorFirstFloorMapId;
    public bool HasHoveredMarker => !string.IsNullOrEmpty(HoveredMarker);
    public SecurityAnalysis SecurityAnalysis { get; }
    public string CalibrationPath => _calibrationStore.FilePath;

    public MainViewModel()
    {
        SecurityAnalysis = SecurityAnalysisLoader.LoadKortz();
        _initialCalibration = FitSecurityData();
        CurrentCalibration = _calibrationStore.Load(ExteriorFirstFloorMapId) ?? _initialCalibration;
        LoadCalibrationFields(CurrentCalibration);
    }

    [RelayCommand]
    private void FitData()
    {
        LoadCalibrationFields(FitSecurityData());
        SaveStatus = "Auto-fit applied (not saved)";
    }

    [RelayCommand]
    private void Reset()
    {
        LoadCalibrationFields(_initialCalibration);
        SaveStatus = "Reset to initial fit (not saved)";
    }

    [RelayCommand]
    private void SaveCalibration()
    {
        _calibrationStore.Save(ExteriorFirstFloorMapId, CurrentCalibration);
        SaveStatus = "Calibration saved";
    }

    partial void OnSelectedMapChanged(MapDefinition value)
    {
        HoveredMarker = null;
        SaveStatus = null;
    }

    partial void OnScaleXChanged(double value) => UpdateCalibration();
    partial void OnScaleYChanged(double value) => UpdateCalibration();
    partial void OnOffsetXChanged(double value) => UpdateCalibration();
    partial void OnOffsetYChanged(double value) => UpdateCalibration();
    partial void OnRotationDegreesChanged(double value) => UpdateCalibration();
    partial void OnFlipXChanged(bool value) => UpdateCalibration();
    partial void OnFlipYChanged(bool value) => UpdateCalibration();

    private MapCalibration FitSecurityData()
    {
        var points = SecurityAnalysis.SecurityScenarioPoints
            .Select(point => new MapPoint(point.Position.X, point.Position.Y))
            .Concat(SecurityAnalysis.GuardChains.Where(chain => chain.ChainIndex == 7)
                .SelectMany(chain => chain.Nodes)
                .Select(node => new MapPoint(node.Position.X, node.Position.Y)));
        return MapCalibrationFitter.Fit(points, 0.1);
    }

    private void LoadCalibrationFields(MapCalibration calibration)
    {
        _updatingCalibrationFields = true;
        ScaleX = calibration.ScaleX;
        ScaleY = calibration.ScaleY;
        OffsetX = calibration.OffsetX;
        OffsetY = calibration.OffsetY;
        RotationDegrees = calibration.RotationDegrees;
        FlipX = calibration.FlipX;
        FlipY = calibration.FlipY;
        CurrentCalibration = calibration;
        _updatingCalibrationFields = false;
    }

    private void UpdateCalibration()
    {
        if (_updatingCalibrationFields || CurrentCalibration is null)
            return;
        CurrentCalibration = CurrentCalibration with
        {
            ScaleX = ScaleX,
            ScaleY = ScaleY,
            OffsetX = OffsetX,
            OffsetY = OffsetY,
            RotationDegrees = RotationDegrees,
            FlipX = FlipX,
            FlipY = FlipY,
        };
        SaveStatus = "Unsaved changes";
    }
}
