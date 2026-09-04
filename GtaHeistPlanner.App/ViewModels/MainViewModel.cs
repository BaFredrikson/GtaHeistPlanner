using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GtaHeistPlanner.App.Models;
using GtaHeistPlanner.App.Services;
using GtaHeistPlanner.Core.Maps;
using GtaHeistPlanner.Core.Loot;
using GtaHeistPlanner.Core.Overlays;
using GtaHeistPlanner.Core.Planning;
using GtaHeistPlanner.Core.Settings;
using GtaHeistPlanner.Core.Sewer;

namespace GtaHeistPlanner.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public const string ExteriorFirstFloorMapId = "exterior-firstfloor";
    private const bool HasAuthoritativeSecurityData = false;
    private readonly MapCalibrationStore _calibrationStore = new();
    private readonly LootSpawnStore _lootSpawnStore = new();
    private readonly ApplicationSettingsStore _settingsStore = new();
    private readonly RecordingDeviceService _recordingDeviceService = new();
    private readonly SewerGraphStore _sewerGraphStore = new();
    private readonly MapCalibration _initialCalibration;
    private ApplicationSettings? _settingsSnapshot;
    private LootRunState _lootRun = new([]);
    private bool _updatingCalibrationFields;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedMapAssetUri))]
    [NotifyPropertyChangedFor(nameof(IsSecurityOverlayVisible))]
    [NotifyPropertyChangedFor(nameof(ShowLootOverlay))]
    [NotifyPropertyChangedFor(nameof(IsLootAuthoringEnabled))]
    [NotifyPropertyChangedFor(nameof(ShowDeveloperCalibration))]
    [NotifyPropertyChangedFor(nameof(ShowSecurityOverlay))]
    [NotifyPropertyChangedFor(nameof(ShowCameras))]
    [NotifyPropertyChangedFor(nameof(ShowCameraCones))]
    [NotifyPropertyChangedFor(nameof(IsSewerSelected))]
    [NotifyPropertyChangedFor(nameof(IsSewerEditorVisible))]
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
    [NotifyPropertyChangedFor(nameof(CurrentPolicy))]
    [NotifyPropertyChangedFor(nameof(ShowLootOverlay))]
    [NotifyPropertyChangedFor(nameof(ShowUnknownLootClearly))]
    [NotifyPropertyChangedFor(nameof(StageTitle))]
    [NotifyPropertyChangedFor(nameof(StageDescription))]
    [NotifyPropertyChangedFor(nameof(IsLootStage))]
    [NotifyPropertyChangedFor(nameof(ShowSecurityOverlay))]
    [NotifyPropertyChangedFor(nameof(ShowCameras))]
    [NotifyPropertyChangedFor(nameof(ShowCameraCones))]
    [NotifyPropertyChangedFor(nameof(SelectedStageOption))]
    [NotifyPropertyChangedFor(nameof(IsPlanningStage))]
    [NotifyPropertyChangedFor(nameof(IsMapStage))]
    public partial PlannerStage CurrentStage { get; set; } = PlannerStage.Preparation;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPlayerCountOption))]
    public partial int PlayerCount { get; set; } = 1;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MicrophoneColor))]
    [NotifyPropertyChangedFor(nameof(MicrophoneStatusText))]
    public partial MicrophoneStatus MicrophoneStatus { get; set; } = MicrophoneStatus.Off;
    [ObservableProperty] public partial bool IsSettingsOpen { get; set; }
    [ObservableProperty] public partial bool IsMenuOpen { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLootAuthoringEnabled))]
    [NotifyPropertyChangedFor(nameof(ShowDeveloperCalibration))]
    [NotifyPropertyChangedFor(nameof(IsSewerEditorVisible))]
    public partial bool DeveloperMode { get; set; }
    [ObservableProperty] public partial bool MicrophoneEnabled { get; set; } = true;
    [ObservableProperty] public partial RecordingDeviceInfo? SelectedRecordingDevice { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHoveredMarker))]
    public partial string? HoveredMarker { get; set; }
    [ObservableProperty] public partial string? SaveStatus { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLootAuthoringEnabled))]
    public partial bool IsLootEditMode { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedLoot))]
    public partial LootMarkerViewModel? SelectedLoot { get; set; }
    [ObservableProperty] public partial string? LootStatus { get; set; }
    [ObservableProperty] public partial int LootRevision { get; set; }
    [ObservableProperty] public partial SewerNodeViewModel? SelectedSewerNode { get; set; }
    [ObservableProperty] public partial SewerNodeViewModel? SewerConnectionTarget { get; set; }
    [ObservableProperty] public partial SewerTurn NewSewerConnectionTurn { get; set; }
    [ObservableProperty] public partial string SewerRouteInput { get; set; } = string.Empty;
    [ObservableProperty] public partial string? SewerDiagnostic { get; set; }
    [ObservableProperty] public partial int SewerRevision { get; set; }
    [ObservableProperty] public partial string? SewerStartNodeId { get; set; }

    public ObservableCollection<MapDefinition> Maps { get; } = [];
    public ObservableCollection<LootMarkerViewModel> LootMarkers { get; } = [];
    public ObservableCollection<RecordingDeviceInfo> RecordingDevices { get; } = [];
    public ObservableCollection<SewerNodeViewModel> SewerNodes { get; } = [];
    public ObservableCollection<SewerConnection> SewerConnections { get; } = [];
    public ObservableCollection<SewerConnection> HighlightedSewerConnections { get; } = [];
    public IReadOnlyList<SewerTurn> SewerTurns { get; } = Enum.GetValues<SewerTurn>();
    public IReadOnlyList<PlayerCountOption> PlayerCountOptions { get; } =
    [
        new(1, "Solo"), new(2, "2"), new(3, "3"), new(4, "4"),
    ];
    public IReadOnlyList<PlannerStageOption> StageOptions { get; } =
    [
        new(PlannerStage.Preparation, "Preparation"),
        new(PlannerStage.Planning, "Planning"),
        new(PlannerStage.HeistInfiltration, "Infiltration"),
        new(PlannerStage.HeistActivity, "Activity"),
    ];
    public IReadOnlyList<LootEconomics> LootTypes { get; } = LootEconomicsCatalog.Standard;
    public string SelectedMapAssetUri => $"avares://GtaHeistPlanner.App/{SelectedMap.SvgAssetPath}";
    public StageViewPolicy CurrentPolicy => StageViewPolicies.Get(CurrentStage);
    public bool ShowLootOverlay => CurrentPolicy.AllowsOverlay(OverlayType.Loot, SelectedMap.Id);
    public bool ShowUnknownLootClearly => CurrentPolicy.LootVisibility == LootVisibilityMode.AllClearly;
    public bool IsLootAuthoringEnabled => IsLootEditMode &&
        DeveloperViewPolicy.CanAuthorLoot(DeveloperMode, CurrentPolicy, SelectedMap.Id);
    public bool ShowDeveloperCalibration => DeveloperMode && SelectedMap.Id == ExteriorFirstFloorMapId;
    public bool IsLootStage => ShowLootOverlay;
    public bool IsPlanningStage => CurrentStage == PlannerStage.Planning;
    public bool IsMapStage => !IsPlanningStage;
    public bool IsSewerSelected => SelectedMap.Id == "sewer";
    public bool IsSewerEditorVisible => DeveloperMode && IsSewerSelected;
    public LootPlanningSummary PlanningSummary => LootPlanningSummaryCalculator.Calculate(
        LootMarkers.Select(marker => marker.ToDefinition()), _lootRun.States, PlayerCount);
    public string PlanningValueRange => $"${PlanningSummary.EstimatedMinValue:N0} – ${PlanningSummary.EstimatedMaxValue:N0}";
    public string StageTitle => StageOptions.Single(option => option.Stage == CurrentStage).Label;
    public string StageDescription => CurrentStage switch
    {
        PlannerStage.Preparation => "Scope and record loot presence and Buyer's Requests.",
        PlannerStage.Planning => "Review scoped loot and gathered information.",
        PlannerStage.HeistInfiltration => "Exterior navigation, entries, guards, and cameras.",
        PlannerStage.HeistActivity => "Internal navigation and live scoped-loot status.",
        _ => string.Empty,
    };
    public string MicrophoneColor => MicrophoneStatus switch
    {
        MicrophoneStatus.Off => "#687484",
        MicrophoneStatus.Listening => "#E24B4B",
        MicrophoneStatus.InputDetected => "#52E36D",
        _ => "#687484",
    };
    public string MicrophoneStatusText => MicrophoneStatus switch
    {
        MicrophoneStatus.Off => "Microphone off",
        MicrophoneStatus.Listening => "Microphone listening",
        MicrophoneStatus.InputDetected => "Microphone input detected",
        _ => "Microphone",
    };
    public PlayerCountOption SelectedPlayerCountOption
    {
        get => PlayerCountOptions.Single(option => option.Count == PlayerCount);
        set => PlayerCount = value.Count;
    }
    public PlannerStageOption SelectedStageOption
    {
        get => StageOptions.Single(option => option.Stage == CurrentStage);
        set => CurrentStage = value.Stage;
    }
    // Extracted security data remains disabled until replaced with map-scoped manual data.
    public bool IsSecurityOverlayVisible => false;
    public bool ShowSecurityOverlay => HasAuthoritativeSecurityData &&
        (CurrentPolicy.AllowsOverlay(OverlayType.ExteriorGuards, SelectedMap.Id) ||
         CurrentPolicy.AllowsOverlay(OverlayType.InteriorGuards, SelectedMap.Id));
    public bool ShowCameras => HasAuthoritativeSecurityData &&
        (CurrentPolicy.AllowsOverlay(OverlayType.ExteriorCameras, SelectedMap.Id) ||
         CurrentPolicy.AllowsOverlay(OverlayType.InteriorCameras, SelectedMap.Id));
    public bool ShowCameraCones => ShowCameras;
    public bool HasHoveredMarker => !string.IsNullOrEmpty(HoveredMarker);
    public bool HasSelectedLoot => SelectedLoot is not null;
    public string BuyersRequestStatus =>
        $"Buyer's Request: {_lootRun.BuyersRequestCount} / {LootRunState.BuyersRequestLimit} identified";
    public SecurityAnalysis SecurityAnalysis { get; }
    public CameraOverlayData CameraData { get; }
    public string CalibrationPath => _calibrationStore.FilePath;
    public string LootLayoutPath => _lootSpawnStore.FilePath;
    public string SettingsPath => _settingsStore.FilePath;
    public string SewerGraphPath => _sewerGraphStore.FilePath;

    public MainViewModel()
    {
        LoadSettings();
        ApplyStagePolicy();
        SecurityAnalysis = SecurityAnalysisLoader.LoadKortz();
        CameraData = CameraOverlayDataLoader.LoadKortzExterior();
        if (CameraData.MapId != ExteriorFirstFloorMapId)
            throw new InvalidDataException($"Camera data targets '{CameraData.MapId}', expected '{ExteriorFirstFloorMapId}'.");
        _initialCalibration = FitSecurityData();
        CurrentCalibration = _calibrationStore.Load(ExteriorFirstFloorMapId) ?? _initialCalibration;
        LoadCalibrationFields(CurrentCalibration);
        LoadLootLayout();
        LoadSewerGraph();
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
        SelectedLoot = null;
        SaveStatus = null;
    }

    partial void OnCurrentStageChanged(PlannerStage value)
    {
        ApplyStagePolicy();
        SelectedLoot = null;
        HoveredMarker = null;
        OnPropertyChanged(nameof(ShowLootOverlay));
        OnPropertyChanged(nameof(IsLootAuthoringEnabled));
    }

    partial void OnPlayerCountChanging(int value)
    {
        if (value is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(value), "Player count must be between 1 and 4.");
    }

    partial void OnPlayerCountChanged(int value) => RefreshPlanningSummary();

    partial void OnDeveloperModeChanged(bool value)
    {
        if (!value)
            IsLootEditMode = false;
    }

    partial void OnMicrophoneEnabledChanged(bool value)
    {
        if (!value)
            MicrophoneStatus = MicrophoneStatus.Off;
    }

    [RelayCommand]
    private void ToggleMicrophone()
    {
        if (!MicrophoneEnabled)
            return;
        MicrophoneStatus = MicrophoneStatus == MicrophoneStatus.Off
            ? MicrophoneStatus.Listening
            : MicrophoneStatus.Off;
    }

    public void SetMicrophoneInputDetected(bool detected)
    {
        if (!MicrophoneEnabled || MicrophoneStatus == MicrophoneStatus.Off)
            return;
        MicrophoneStatus = detected ? MicrophoneStatus.InputDetected : MicrophoneStatus.Listening;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        _settingsSnapshot = new ApplicationSettings
        {
            RecordingDeviceId = SelectedRecordingDevice?.Id,
            RecordingDeviceName = SelectedRecordingDevice?.Name,
            MicrophoneEnabled = MicrophoneEnabled,
            DeveloperMode = DeveloperMode,
        };
        RefreshRecordingDevices();
        IsMenuOpen = false;
        IsSettingsOpen = true;
    }

    [RelayCommand]
    private void ToggleMenu() => IsMenuOpen = !IsMenuOpen;

    [RelayCommand]
    private void CloseSettings()
    {
        if (_settingsSnapshot is not null)
        {
            DeveloperMode = _settingsSnapshot.DeveloperMode;
            MicrophoneEnabled = _settingsSnapshot.MicrophoneEnabled;
            RefreshRecordingDevices(_settingsSnapshot.RecordingDeviceId, _settingsSnapshot.RecordingDeviceName);
        }
        _settingsSnapshot = null;
        IsSettingsOpen = false;
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settingsStore.Save(new ApplicationSettings
        {
            RecordingDeviceId = SelectedRecordingDevice?.Id,
            RecordingDeviceName = SelectedRecordingDevice?.Name,
            MicrophoneEnabled = MicrophoneEnabled,
            DeveloperMode = DeveloperMode,
        });
        _settingsSnapshot = null;
        IsSettingsOpen = false;
    }

    [RelayCommand]
    private void CreateLoot(MapPoint position)
    {
        if (!IsLootEditMode)
            return;

        var sequence = 1;
        string id;
        do id = $"{SelectedMap.Id}-loot-{sequence++:00}";
        while (LootMarkers.Any(marker => marker.Id == id));

        var definition = new LootSpawnDefinition(id, SelectedMap.Id, $"Loot {sequence - 1:00}", LootType.CoquardJewelry,
            Math.Clamp(position.X, 0, 1), Math.Clamp(position.Y, 0, 1));
        RebuildRunState(LootMarkers.Select(marker => marker.ToDefinition()).Append(definition));
        SelectedLoot = LootMarkers.Single(marker => marker.Id == id);
        _lootRun.SetLootPresent(id, true);
        SelectedLoot.ApplyState(_lootRun.GetState(id));
        LootRevision++;
        LootStatus = "Loot marker added; edit its name/type, then save the layout.";
    }

    [RelayCommand]
    private void MoveLoot(LootMarkerMove move)
    {
        var marker = LootMarkers.FirstOrDefault(item => item.Id == move.SpawnId);
        if (marker is null || !IsLootEditMode)
            return;
        marker.X = Math.Clamp(move.X, 0, 1);
        marker.Y = Math.Clamp(move.Y, 0, 1);
        LootRevision++;
        LootStatus = "Unsaved layout changes";
    }

    [RelayCommand]
    private void SelectLoot(string spawnId) =>
        SelectedLoot = LootMarkers.FirstOrDefault(marker => marker.Id == spawnId);

    [RelayCommand]
    private void RemoveSelectedLoot()
    {
        if (SelectedLoot is null || !IsLootEditMode)
            return;
        var id = SelectedLoot.Id;
        RebuildRunState(LootMarkers.Where(marker => marker.Id != id).Select(marker => marker.ToDefinition()));
        SelectedLoot = null;
        LootRevision++;
        LootStatus = "Loot marker removed; save to persist the change.";
    }

    [RelayCommand]
    private void SaveLootLayout()
    {
        _lootSpawnStore.Save(LootMarkers.Select(marker => marker.ToDefinition()));
        LootStatus = "Loot layout saved";
    }

    [RelayCommand]
    private void ResetHeist()
    {
        _lootRun.ResetLootState();
        RefreshLootState();
        LootStatus = "Heist loot state reset; permanent layout unchanged.";
    }

    [RelayCommand]
    private void ToggleLootPresent()
    {
        if (SelectedLoot is null)
            return;
        _lootRun.SetLootPresent(SelectedLoot.Id, !SelectedLoot.IsPresent);
        SelectedLoot.ApplyState(_lootRun.GetState(SelectedLoot.Id));
        LootRevision++;
        LootStatus = SelectedLoot.IsPresent ? "Loot marked present" : "Loot marked absent";
        RefreshPlanningSummary();
    }

    [RelayCommand]
    private void ToggleBuyersRequest()
    {
        if (SelectedLoot is null)
            return;
        if (!_lootRun.TrySetBuyersRequest(SelectedLoot.Id, !SelectedLoot.IsBuyersRequest, out var error))
        {
            LootStatus = error;
            return;
        }
        SelectedLoot.ApplyState(_lootRun.GetState(SelectedLoot.Id));
        LootRevision++;
        OnPropertyChanged(nameof(BuyersRequestStatus));
        LootStatus = SelectedLoot.IsBuyersRequest ? "Buyer's Request identified" : "Buyer's Request cleared";
        RefreshPlanningSummary();
    }

    [RelayCommand]
    private void ToggleLooted()
    {
        if (SelectedLoot is null)
            return;
        _lootRun.SetLooted(SelectedLoot.Id, !SelectedLoot.IsLooted);
        SelectedLoot.ApplyState(_lootRun.GetState(SelectedLoot.Id));
        LootRevision++;
        LootStatus = SelectedLoot.IsLooted ? "Loot marked collected" : "Loot restored";
    }

    private void LoadLootLayout() => RebuildRunState(_lootSpawnStore.Load());

    private void LoadSewerGraph()
    {
        var graph = _sewerGraphStore.Load();
        SewerStartNodeId = graph.StartNodeId;
        SewerNodes.Clear();
        foreach (var node in graph.Nodes)
            SewerNodes.Add(new SewerNodeViewModel(node));
        SewerConnections.Clear();
        foreach (var connection in graph.Connections)
            SewerConnections.Add(connection);
        SewerRevision++;
    }

    public void SetSewerRoute(IEnumerable<SewerTurn> turns)
    {
        var result = SewerGraphTraversal.Traverse(BuildSewerGraph(), new SewerRoute(turns.ToList()));
        HighlightedSewerConnections.Clear();
        foreach (var connection in result.TraversedConnections)
            HighlightedSewerConnections.Add(connection);
        SewerRevision++;
        SewerDiagnostic = result.IsComplete ? "Sewer route resolved." : result.Error;
    }

    [RelayCommand]
    private void ApplySewerRoute()
    {
        try
        {
            SetSewerRoute(SewerRouteParser.Parse(SewerRouteInput).Turns);
        }
        catch (Exception exception) when (exception is FormatException or InvalidDataException)
        {
            HighlightedSewerConnections.Clear();
            SewerRevision++;
            SewerDiagnostic = exception.Message;
        }
    }

    [RelayCommand]
    private void AddSewerNode(MapPoint point)
    {
        if (!IsSewerEditorVisible) return;
        var sequence = 1;
        string id;
        do id = $"junction-{sequence++:00}"; while (SewerNodes.Any(node => node.Id == id));
        var node = new SewerNodeViewModel(new SewerNode(id, point.X, point.Y));
        SewerNodes.Add(node);
        SelectedSewerNode = node;
        SewerRevision++;
        SewerDiagnostic = "Junction added; save the graph to persist it.";
    }

    [RelayCommand]
    private void MoveSewerNode(SewerNodeMove move)
    {
        if (!IsSewerEditorVisible) return;
        var node = SewerNodes.FirstOrDefault(item => item.Id == move.NodeId);
        if (node is null) return;
        node.MapX = Math.Clamp(move.X, 0, 1);
        node.MapY = Math.Clamp(move.Y, 0, 1);
        SewerRevision++;
    }

    [RelayCommand]
    private void SelectSewerNode(string id) =>
        SelectedSewerNode = SewerNodes.FirstOrDefault(node => node.Id == id);

    [RelayCommand]
    private void SetSewerStartNode()
    {
        if (SelectedSewerNode is null) return;
        SewerStartNodeId = SelectedSewerNode.Id;
        SewerRevision++;
    }

    [RelayCommand]
    private void AddSewerConnection()
    {
        if (SelectedSewerNode is null || SewerConnectionTarget is null) return;
        var connection = new SewerConnection(SelectedSewerNode.Id, SewerConnectionTarget.Id, NewSewerConnectionTurn);
        if (SewerConnections.Any(item => item.FromNodeId == connection.FromNodeId && item.Turn == connection.Turn))
        {
            SewerDiagnostic = $"{connection.FromNodeId} already has a {connection.Turn} connection.";
            return;
        }
        SewerConnections.Add(connection);
        SewerRevision++;
        SewerDiagnostic = "Connection added; save the graph to persist it.";
    }

    [RelayCommand]
    private void SaveSewerGraph()
    {
        try
        {
            _sewerGraphStore.Save(BuildSewerGraph());
            SewerDiagnostic = "Sewer graph saved.";
        }
        catch (InvalidDataException exception)
        {
            SewerDiagnostic = exception.Message;
        }
    }

    private SewerGraph BuildSewerGraph() => new(
        SewerStartNodeId,
        SewerNodes.Select(node => node.ToDomain()).ToList(),
        SewerConnections.ToList());

    private void ApplyStagePolicy()
    {
        var policy = CurrentPolicy;
        var selectedId = SelectedMap?.Id;
        Maps.Clear();
        foreach (var map in KortzMapCatalog.Maps.Where(map => policy.AllowsMap(map.Id)))
            Maps.Add(map);

        if (selectedId is null || !policy.AllowsMap(selectedId))
            SelectedMap = Maps.First();
    }

    private void LoadSettings()
    {
        var settings = _settingsStore.Load();
        DeveloperMode = settings.DeveloperMode;
        MicrophoneEnabled = settings.MicrophoneEnabled;
        RefreshRecordingDevices(settings.RecordingDeviceId, settings.RecordingDeviceName);
    }

    private void RefreshRecordingDevices(string? preferredId = null, string? preferredName = null)
    {
        preferredId ??= SelectedRecordingDevice?.Id;
        preferredName ??= SelectedRecordingDevice?.Name;
        RecordingDevices.Clear();
        foreach (var device in _recordingDeviceService.GetAvailableDevices())
            RecordingDevices.Add(device);

        SelectedRecordingDevice = RecordingDevices.FirstOrDefault(device => device.Id == preferredId);
        if (SelectedRecordingDevice is null && !string.IsNullOrWhiteSpace(preferredId))
        {
            SelectedRecordingDevice = new RecordingDeviceInfo(preferredId, preferredName ?? "Unavailable recording device");
            RecordingDevices.Add(SelectedRecordingDevice);
        }
        SelectedRecordingDevice ??= RecordingDevices.FirstOrDefault();
    }

    private void RebuildRunState(IEnumerable<LootSpawnDefinition> definitions)
    {
        var previous = _lootRun.States.ToDictionary(state => state.SpawnId, StringComparer.Ordinal);
        _lootRun = new LootRunState(definitions);
        foreach (var state in _lootRun.States)
        {
            if (!previous.TryGetValue(state.SpawnId, out var oldState))
                continue;
            state.IsPresent = oldState.IsPresent;
            state.IsBuyersRequest = oldState.IsBuyersRequest;
            state.IsLooted = oldState.IsLooted;
            state.ScopedValue = oldState.ScopedValue;
        }
        foreach (var marker in LootMarkers)
            marker.PropertyChanged -= OnLootMarkerPropertyChanged;
        LootMarkers.Clear();
        foreach (var definition in _lootRun.Definitions)
        {
            var marker = new LootMarkerViewModel(definition, _lootRun.GetState(definition.Id));
            marker.PropertyChanged += OnLootMarkerPropertyChanged;
            LootMarkers.Add(marker);
        }
        LootRevision++;
        OnPropertyChanged(nameof(BuyersRequestStatus));
        RefreshPlanningSummary();
    }

    private void OnLootMarkerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LootMarkerViewModel.Type) or nameof(LootMarkerViewModel.ZoneId))
            LootRevision++;
        if (e.PropertyName is nameof(LootMarkerViewModel.Name) or nameof(LootMarkerViewModel.Type) or nameof(LootMarkerViewModel.ZoneId))
            LootStatus = "Unsaved layout changes";
        if (e.PropertyName is nameof(LootMarkerViewModel.Type) or nameof(LootMarkerViewModel.ZoneId) or nameof(LootMarkerViewModel.ScopedValue))
            RefreshPlanningSummary();
    }

    private void RefreshLootState()
    {
        foreach (var marker in LootMarkers)
            marker.ApplyState(_lootRun.GetState(marker.Id));
        LootRevision++;
        OnPropertyChanged(nameof(BuyersRequestStatus));
        RefreshPlanningSummary();
    }

    private void RefreshPlanningSummary()
    {
        OnPropertyChanged(nameof(PlanningSummary));
        OnPropertyChanged(nameof(PlanningValueRange));
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
