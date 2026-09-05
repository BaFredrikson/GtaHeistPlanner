using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using GtaHeistPlanner.App.Models;
using GtaHeistPlanner.App.Services;
using GtaHeistPlanner.Core.Maps;
using GtaHeistPlanner.Core.Loot;
using GtaHeistPlanner.Core.Overlays;
using GtaHeistPlanner.Core.Planning;
using GtaHeistPlanner.Core.Settings;
using GtaHeistPlanner.Core.Sewer;
using GtaHeistPlanner.Core.Security;
using GtaHeistPlanner.Voice;

namespace GtaHeistPlanner.App.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    public const string ExteriorFirstFloorMapId = "exterior-firstfloor";
    private readonly MapCalibrationStore _calibrationStore = new();
    private readonly LootSpawnStore _lootSpawnStore = new();
    private readonly ApplicationSettingsStore _settingsStore = new();
    private readonly RecordingDeviceService _recordingDeviceService = new();
    private readonly SewerGraphStore _sewerGraphStore = new();
    private readonly SecurityDatasetStore _securityDatasetStore = new();
    private readonly MapCalibration _initialCalibration;
    private ApplicationSettings? _settingsSnapshot;
    private LootRunState _lootRun = new([]);
    private bool _updatingCalibrationFields;
    private readonly ISpeechRecognitionService _speechRecognitionService;
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly DispatcherTimer _inputDetectedResetTimer;
    private ScopeOutVoiceSession? _scopeOutSession;

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
    [NotifyPropertyChangedFor(nameof(ShowManualCameras))]
    [NotifyPropertyChangedFor(nameof(ShowManualGuards))]
    [NotifyPropertyChangedFor(nameof(IsSecurityEditorVisible))]
    [NotifyPropertyChangedFor(nameof(CurrentMapCameras))]
    [NotifyPropertyChangedFor(nameof(CurrentMapGuards))]
    [NotifyPropertyChangedFor(nameof(CurrentMapPatrols))]
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
    [NotifyPropertyChangedFor(nameof(ShowManualCameras))]
    [NotifyPropertyChangedFor(nameof(ShowManualGuards))]
    [NotifyPropertyChangedFor(nameof(IsSecurityEditorVisible))]
    [NotifyPropertyChangedFor(nameof(SelectedStageOption))]
    [NotifyPropertyChangedFor(nameof(IsPlanningStage))]
    [NotifyPropertyChangedFor(nameof(IsMapStage))]
    [NotifyPropertyChangedFor(nameof(IsActivityStage))]
    [NotifyPropertyChangedFor(nameof(IsPreparationStage))]
    public partial PlannerStage CurrentStage { get; set; } = PlannerStage.Preparation;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPlayerCountOption))]
    public partial int PlayerCount { get; set; } = 1;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MicrophoneColor))]
    [NotifyPropertyChangedFor(nameof(MicrophoneStatusText))]
    [NotifyPropertyChangedFor(nameof(IsMicrophoneOpen))]
    public partial MicrophoneStatus MicrophoneStatus { get; set; } = MicrophoneStatus.Off;
    [ObservableProperty] public partial bool IsSettingsOpen { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLootAuthoringEnabled))]
    [NotifyPropertyChangedFor(nameof(ShowDeveloperCalibration))]
    [NotifyPropertyChangedFor(nameof(IsSecurityEditorVisible))]
    public partial bool DeveloperMode { get; set; }
    [ObservableProperty] public partial bool MicrophoneEnabled { get; set; } = true;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfiguredCaptureDevice))]
    public partial RecordingDeviceInfo? SelectedRecordingDevice { get; set; }
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
    [ObservableProperty] public partial string SewerRouteInput { get; set; } = string.Empty;
    [ObservableProperty] public partial string? SewerDiagnostic { get; set; }
    [ObservableProperty] public partial int SewerRevision { get; set; }
    [ObservableProperty] public partial int? SewerStartChamber { get; set; }
    [ObservableProperty] public partial string? SewerEntrancePathId { get; set; }
    [ObservableProperty] public partial int SewerExitChamber { get; set; } = 4;
    [ObservableProperty] public partial string? SewerExitPathId { get; set; }
    [ObservableProperty] public partial bool IsSewerRouteComplete { get; set; }
    [ObservableProperty] public partial SecurityEditorTool SecurityEditorTool { get; set; }
    [ObservableProperty] public partial SecurityCameraViewModel? SelectedSecurityCamera { get; set; }
    [ObservableProperty] public partial SecurityGuardViewModel? SelectedSecurityGuard { get; set; }
    [ObservableProperty] public partial SecurityPatrolViewModel? SelectedSecurityPatrol { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedPatrolWaypoint))]
    public partial int SelectedPatrolWaypointIndex { get; set; } = -1;
    [ObservableProperty] public partial int SecurityRevision { get; set; }
    [ObservableProperty] public partial string? SecurityStatus { get; set; }
    [ObservableProperty] public partial string? VaultCode { get; set; }
    [ObservableProperty] public partial string VoiceRecognitionState { get; set; } = "Stopped";
    [ObservableProperty] public partial string? LastRecognizedText { get; set; }
    [ObservableProperty] public partial string? LastParsedVoiceCommand { get; set; }
    [ObservableProperty] public partial string? VoiceCommandError { get; set; }
    [ObservableProperty] public partial string? LastVoiceLootUpdate { get; set; }
    [ObservableProperty] public partial string VoiceSimulatorText { get; set; } = string.Empty;
    [ObservableProperty] public partial string? VoiceStartupDiagnostics { get; set; }
    [ObservableProperty] public partial string? CurrentPartialTranscript { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFocusedMap))]
    [NotifyPropertyChangedFor(nameof(FocusedMapCard))]
    public partial string? FocusedMapId { get; set; }

    public ObservableCollection<MapDefinition> Maps { get; } = [];
    public ObservableCollection<LootMarkerViewModel> LootMarkers { get; } = [];
    public ObservableCollection<RecordingDeviceInfo> RecordingDevices { get; } = [];
    public ObservableCollection<VoiceTranscriptEntry> VoiceTranscript { get; } = [];
    public ObservableCollection<SewerPath> SewerPaths { get; } = [];
    public ObservableCollection<SewerConnection> SewerConnections { get; } = [];
    public ObservableCollection<string> HighlightedSewerPathIds { get; } = [];
    public ObservableCollection<SecurityCameraViewModel> SecurityCameras { get; } = [];
    public ObservableCollection<SecurityGuardViewModel> SecurityGuards { get; } = [];
    public ObservableCollection<SecurityPatrolViewModel> SecurityPatrols { get; } = [];
    public ObservableCollection<MapCardViewModel> VisibleMapCards { get; } = [];
    public ObservableCollection<PatrolAssignmentViewModel> SelectedGuardPatrolAssignments { get; } = [];
    public IReadOnlyList<SecurityEditorTool> SecurityEditorTools { get; } = Enum.GetValues<SecurityEditorTool>();
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
    public bool IsActivityStage => CurrentStage == PlannerStage.HeistActivity;
    public bool IsPreparationStage => CurrentStage == PlannerStage.Preparation;
    public bool HasSelectedPatrolWaypoint => SelectedSecurityPatrol is not null && SelectedPatrolWaypointIndex >= 0;
    public bool HasFocusedMap => FocusedMapId is not null;
    public MapCardViewModel? FocusedMapCard => VisibleMapCards.FirstOrDefault(card => card.Map.Id == FocusedMapId);
    public bool IsSewerSelected => SelectedMap.Id == "sewer";
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
    public bool IsMicrophoneOpen => MicrophoneStatus != MicrophoneStatus.Off;
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
    public bool ShowSecurityOverlay => false;
    public bool ShowCameras => false;
    public bool ShowCameraCones => false;
    public bool ShowManualGuards => CurrentPolicy.AllowsOverlay(
        SelectedMap.Category == MapCategory.Exterior ? OverlayType.ExteriorGuards : OverlayType.InteriorGuards, SelectedMap.Id);
    public bool ShowManualCameras => CurrentPolicy.AllowsOverlay(
        SelectedMap.Category == MapCategory.Exterior ? OverlayType.ExteriorCameras : OverlayType.InteriorCameras, SelectedMap.Id);
    public bool IsSecurityEditorVisible => DeveloperViewPolicy.CanAuthorSecurity(DeveloperMode, CurrentStage);
    public IEnumerable<SecurityCameraViewModel> CurrentMapCameras => SecurityCameras.Where(item => item.MapId == SelectedMap.Id);
    public IEnumerable<SecurityGuardViewModel> CurrentMapGuards => SecurityGuards.Where(item => item.MapId == SelectedMap.Id);
    public IEnumerable<SecurityPatrolViewModel> CurrentMapPatrols => SecurityPatrols.Where(item => item.MapId == SelectedMap.Id);
    public bool HasHoveredMarker => !string.IsNullOrEmpty(HoveredMarker);
    public bool HasSelectedLoot => SelectedLoot is not null;
    public string BuyersRequestStatus =>
        $"Buyer's Request: {_lootRun.BuyersRequestCount} / {LootRunState.BuyersRequestLimit} identified";
    public string CalibrationPath => _calibrationStore.FilePath;
    public string LootLayoutPath => _lootSpawnStore.FilePath;
    public string SettingsPath => _settingsStore.FilePath;
    public string SewerGraphPath => _sewerGraphStore.FilePath;
    public string SecurityDataPath => _securityDatasetStore.FilePath;
    public ScopeOutSessionState ScopeOutState => _scopeOutSession?.State ?? ScopeOutSessionState.Inactive;

    public string ConfiguredCaptureDevice => SelectedRecordingDevice is null
        ? "No recording device selected"
        : $"{SelectedRecordingDevice.Name} ({SelectedRecordingDevice.Id})";
    public string TransmittedAudioFormat => "24,000 Hz, 16-bit mono PCM";

    public MainViewModel() : this(new OpenAiRealtimeSpeechRecognitionService(), new WasapiAudioCaptureService())
    {
    }

    public MainViewModel(
        ISpeechRecognitionService speechRecognitionService,
        IAudioCaptureService audioCaptureService)
    {
        _speechRecognitionService = speechRecognitionService;
        _audioCaptureService = audioCaptureService;
        _inputDetectedResetTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _inputDetectedResetTimer.Tick += (_, _) =>
        {
            _inputDetectedResetTimer.Stop();
            SetMicrophoneInputDetected(false);
        };
        _speechRecognitionService.SpeechRecognized += OnSpeechRecognized;
        _speechRecognitionService.PartialTranscriptChanged += OnPartialTranscriptChanged;
        _speechRecognitionService.RecognitionFailed += OnRecognitionFailed;
        _audioCaptureService.FrameCaptured += OnAudioFrameCaptured;
        LoadSettings();
        ApplyStagePolicy();
        _initialCalibration = new MapCalibration();
        CurrentCalibration = _calibrationStore.Load(ExteriorFirstFloorMapId) ?? _initialCalibration;
        LoadCalibrationFields(CurrentCalibration);
        LoadLootLayout();
        LoadSewerGraph();
        LoadSecurityDataset();
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
        SelectedSecurityCamera = null;
        SelectedSecurityGuard = null;
        SelectedSecurityPatrol = null;
        SelectedPatrolWaypointIndex = -1;
        SaveStatus = null;
    }

    partial void OnCurrentStageChanged(PlannerStage value)
    {
        if (value != PlannerStage.Preparation && ScopeOutState == ScopeOutSessionState.Active)
        {
            _scopeOutSession?.SetListening(MicrophoneStatus != MicrophoneStatus.Off);
            OnPropertyChanged(nameof(ScopeOutState));
        }
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
        {
            IsLootEditMode = false;
            SecurityEditorTool = SecurityEditorTool.Select;
        }
    }

    partial void OnSelectedSecurityGuardChanged(SecurityGuardViewModel? oldValue, SecurityGuardViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PatrolIds.Clear();
            foreach (var assignment in SelectedGuardPatrolAssignments.Where(item => item.IsAssigned))
                oldValue.PatrolIds.Add(assignment.Patrol.Id);
        }
        SelectedGuardPatrolAssignments.Clear();
        if (newValue is null) return;
        foreach (var patrol in SecurityPatrols.Where(item => item.MapId == newValue.MapId))
            SelectedGuardPatrolAssignments.Add(new PatrolAssignmentViewModel(patrol, newValue.PatrolIds.Contains(patrol.Id)));
    }

    partial void OnSelectedSecurityPatrolChanged(SecurityPatrolViewModel? value) => SelectedPatrolWaypointIndex = -1;

    partial void OnMicrophoneEnabledChanged(bool value)
    {
        if (!value)
            StopVoiceRecognition();
    }

    [RelayCommand]
    private async Task ToggleMicrophone()
    {
        if (!MicrophoneEnabled)
            return;
        if (MicrophoneStatus == MicrophoneStatus.Off)
            await StartVoiceRecognitionAsync();
        else
            StopVoiceRecognition();
    }

    private async Task StartVoiceRecognitionAsync()
    {
        try
        {
            var endpointId = SelectedRecordingDevice?.Id
                ?? throw new InvalidOperationException("No recording device is selected. Choose one in Settings.");
            _audioCaptureService.Start(endpointId);
            var keywords = LootMarkers
                .SelectMany(marker => marker.VoiceAliases.Append(marker.Name))
                .Concat(["scope out", "stop scope out", "painting", "rings", "loading bay cargo",
                    "safety deposit boxes", "Buyer's Request", "Glass Cutter", "Power Drills", "Coquard"])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            await _speechRecognitionService.StartAsync(_audioCaptureService.Format, keywords);
            MicrophoneStatus = MicrophoneStatus.Listening;
            _scopeOutSession?.SetListening(true);
            VoiceRecognitionState = _speechRecognitionService.StateDescription;
            RefreshVoiceStartupDiagnostics();
            VoiceCommandError = null;
        }
        catch (Exception exception)
        {
            _audioCaptureService.Stop();
            MicrophoneStatus = MicrophoneStatus.Off;
            _scopeOutSession?.SetListening(false);
            VoiceRecognitionState = "Recognition unavailable";
            VoiceCommandError = exception.ToString();
            RefreshVoiceStartupDiagnostics();
        }
        OnPropertyChanged(nameof(ScopeOutState));
    }

    private void StopVoiceRecognition()
    {
        _inputDetectedResetTimer.Stop();
        _audioCaptureService.Stop();
        _speechRecognitionService.Stop();
        MicrophoneStatus = MicrophoneStatus.Off;
        _scopeOutSession?.SetListening(false);
        VoiceRecognitionState = _speechRecognitionService.StateDescription;
        OnPropertyChanged(nameof(ScopeOutState));
    }

    public void SetMicrophoneInputDetected(bool detected)
    {
        if (!MicrophoneEnabled || MicrophoneStatus == MicrophoneStatus.Off)
            return;
        MicrophoneStatus = detected ? MicrophoneStatus.InputDetected : MicrophoneStatus.Listening;
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            CurrentPartialTranscript = null;
            ProcessRecognizedText(e.Text);
        });

    private void OnPartialTranscriptChanged(object? sender, PartialTranscriptEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            CurrentPartialTranscript = e.Text;
            VoiceRecognitionState = _speechRecognitionService.StateDescription;
            RefreshVoiceStartupDiagnostics();
        });

    private void OnRecognitionFailed(object? sender, SpeechRecognitionFailedEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            VoiceCommandError = e.Exception.ToString();
            StopVoiceRecognition();
            VoiceRecognitionState = "Recognition unavailable";
            RefreshVoiceStartupDiagnostics();
        });

    private void OnAudioFrameCaptured(object? sender, PcmAudioFrameEventArgs e)
    {
        _speechRecognitionService.PushAudio(e.Data);
        Dispatcher.UIThread.Post(() =>
        {
            HandleAudioLevel(e.ActivityLevel);
            RefreshVoiceStartupDiagnostics();
        });
    }

    private void RefreshVoiceStartupDiagnostics() => VoiceStartupDiagnostics = string.Join(
        Environment.NewLine,
        _audioCaptureService.Diagnostics.Entries.Concat(_speechRecognitionService.Diagnostics.Entries));

    private void HandleAudioLevel(int level)
    {
        if (level < 3 || MicrophoneStatus == MicrophoneStatus.Off)
            return;
        SetMicrophoneInputDetected(true);
        _inputDetectedResetTimer.Stop();
        _inputDetectedResetTimer.Start();
    }

    [RelayCommand]
    private void SimulateRecognizedText()
    {
        ProcessRecognizedText(VoiceSimulatorText);
    }

    public void ProcessRecognizedText(string text)
    {
        LastRecognizedText = text;
        LastParsedVoiceCommand = null;
        VoiceCommandError = null;

        if (MicrophoneStatus == MicrophoneStatus.Off || _scopeOutSession is null)
        {
            VoiceCommandError = "Microphone is off.";
            AddVoiceTranscript(text, false);
            return;
        }
        if (CurrentStage != PlannerStage.Preparation)
        {
            VoiceCommandError = "Scope-out commands are only available during Preparation.";
            AddVoiceTranscript(text, false);
            return;
        }

        var result = _scopeOutSession.Process(text);
        LastParsedVoiceCommand = result.ParseResult.Command?.GetType().Name
            ?? result.ParseResult.Disposition.ToString();
        VoiceCommandError = result.Error;
        AddVoiceTranscript(text, result.ParseResult.Command is not null);
        OnPropertyChanged(nameof(ScopeOutState));

        if (result.UpdatedLootLocationId is not { } lootId)
            return;
        var marker = LootMarkers.Single(item => item.Id == lootId);
        marker.ApplyState(_lootRun.GetState(lootId));
        SelectedLoot = marker;
        LootRevision++;
        LastVoiceLootUpdate = result.ScopedValue is { } value
            ? $"{lootId} = ${value:N0}"
            : $"{lootId} marked present";
        LootStatus = $"Voice scope-out updated {marker.Name}.";
        RefreshPlanningSummary();
    }

    [RelayCommand]
    private void ClearVoiceTranscript() => VoiceTranscript.Clear();

    private void AddVoiceTranscript(string text, bool isCommand)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        VoiceTranscript.Add(new(text.Trim(), isCommand));
        while (VoiceTranscript.Count > 12)
            VoiceTranscript.RemoveAt(0);
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
        IsSettingsOpen = true;
    }

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
        VaultCode = null;
        foreach (var camera in SecurityCameras) camera.IsActive = true;
        foreach (var guard in SecurityGuards) guard.IsActive = true;
        foreach (var patrol in SecurityPatrols) patrol.IsActive = true;
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
        SewerStartChamber = graph.StartChamber;
        SewerEntrancePathId = graph.EntrancePathId;
        SewerExitChamber = graph.ExitChamber;
        SewerExitPathId = graph.ExitPathId;
        SewerPaths.Clear();
        foreach (var path in graph.Paths)
            SewerPaths.Add(path);
        SewerConnections.Clear();
        foreach (var connection in graph.Connections)
            SewerConnections.Add(connection);
        SewerDiagnostic = _sewerGraphStore.LastLoadWarning;
        SewerRevision++;
    }

    public void SetSewerRoute(IReadOnlyList<SewerInstruction> instructions)
    {
        var result = SewerGraphTraversal.Traverse(BuildSewerGraph(), instructions);
        IsSewerRouteComplete = result.IsComplete;
        HighlightedSewerPathIds.Clear();
        foreach (var pathId in result.PathIds)
            HighlightedSewerPathIds.Add(pathId);
        SewerRevision++;
        SewerDiagnostic = result.IsComplete
            ? $"Sewer route resolved: {string.Join(" → ", result.PathIds)}"
            : result.Error;
    }

    [RelayCommand]
    private void ApplySewerRoute()
    {
        try
        {
            SetSewerRoute(SewerRouteParser.Parse(SewerRouteInput));
        }
        catch (Exception exception) when (exception is FormatException or InvalidDataException)
        {
            HighlightedSewerPathIds.Clear();
            IsSewerRouteComplete = false;
            SewerRevision++;
            SewerDiagnostic = exception.Message;
        }
    }

    private SewerGraph BuildSewerGraph() => new(
        SewerStartChamber,
        SewerPaths.ToList(),
        SewerConnections.ToList(),
        SewerEntrancePathId,
        SewerExitChamber,
        SewerExitPathId);

    private void LoadSecurityDataset()
    {
        var dataset = _securityDatasetStore.Load();
        foreach (var camera in dataset.Cameras)
        {
            var model = new SecurityCameraViewModel(camera);
            model.PropertyChanged += OnSecurityObjectChanged;
            SecurityCameras.Add(model);
        }
        foreach (var guard in dataset.Guards)
        {
            var model = new SecurityGuardViewModel(guard);
            model.PropertyChanged += OnSecurityObjectChanged;
            SecurityGuards.Add(model);
        }
        foreach (var patrol in dataset.Patrols)
        {
            var model = new SecurityPatrolViewModel(patrol);
            model.PropertyChanged += OnSecurityObjectChanged;
            SecurityPatrols.Add(model);
        }
        SecurityRevision++;
    }

    [RelayCommand]
    private void PlaceSecurityObject(MapPoint point)
    {
        if (!IsSecurityEditorVisible) return;
        if (SecurityEditorTool == SecurityEditorTool.Camera)
        {
            var id = NextSecurityId("camera", SecurityCameras.Select(item => item.Id));
            var camera = new SecurityCameraViewModel(new(id, SelectedMap.Id, id, point.X, point.Y, 0, 55, .18));
            camera.PropertyChanged += OnSecurityObjectChanged;
            SecurityCameras.Add(camera);
            SelectedSecurityCamera = camera;
            SelectedSecurityGuard = null;
        }
        else if (SecurityEditorTool == SecurityEditorTool.Guard)
        {
            var id = NextSecurityId("guard", SecurityGuards.Select(item => item.Id));
            var guard = new SecurityGuardViewModel(new(id, SelectedMap.Id, id, point.X, point.Y, []));
            guard.PropertyChanged += OnSecurityObjectChanged;
            SecurityGuards.Add(guard);
            SelectedSecurityGuard = guard;
            SelectedSecurityCamera = null;
        }
        else if (SecurityEditorTool == SecurityEditorTool.PatrolWaypoint && SelectedSecurityPatrol?.MapId == SelectedMap.Id)
        {
            SelectedSecurityPatrol.Waypoints.Add(new PatrolWaypoint(point.X, point.Y));
        }
        RefreshSecurityMapCollections();
        SecurityRevision++;
        SecurityStatus = "Unsaved security changes.";
    }

    [RelayCommand]
    private void MoveSecurityObject(SecurityMarkerMove move)
    {
        if (!IsSecurityEditorVisible) return;
        if (move.Kind == "camera" && SecurityCameras.FirstOrDefault(item => item.Id == move.Id) is { } camera)
        { camera.X = move.X; camera.Y = move.Y; }
        if (move.Kind == "guard" && SecurityGuards.FirstOrDefault(item => item.Id == move.Id) is { } guard)
        { guard.X = move.X; guard.Y = move.Y; }
        SecurityRevision++;
    }

    [RelayCommand]
    private void SelectSecurityCamera(string id)
    {
        if (!IsSecurityEditorVisible) return;
        SelectedSecurityCamera = SecurityCameras.FirstOrDefault(item => item.Id == id);
        SelectedSecurityGuard = null;
    }

    [RelayCommand]
    private void SelectSecurityGuard(string id)
    {
        if (!IsSecurityEditorVisible) return;
        SelectedSecurityGuard = SecurityGuards.FirstOrDefault(item => item.Id == id);
        SelectedSecurityCamera = null;
    }

    [RelayCommand]
    private void NewSecurityPatrol()
    {
        if (!IsSecurityEditorVisible) return;
        var id = NextSecurityId("patrol", SecurityPatrols.Select(item => item.Id));
        var patrol = new SecurityPatrolViewModel(new(id, SelectedMap.Id, id, []));
        patrol.PropertyChanged += OnSecurityObjectChanged;
        SecurityPatrols.Add(patrol);
        SelectedSecurityPatrol = patrol;
        SecurityEditorTool = SecurityEditorTool.PatrolWaypoint;
        RefreshSecurityMapCollections();
        SecurityRevision++;
    }

    [RelayCommand]
    private void RemoveSelectedCamera()
    {
        if (!IsSecurityEditorVisible || SelectedSecurityCamera is null) return;
        SecurityCameras.Remove(SelectedSecurityCamera);
        SelectedSecurityCamera = null;
        RefreshSecurityMapCollections();
        SecurityRevision++;
    }

    [RelayCommand]
    private void RemoveSelectedGuard()
    {
        if (!IsSecurityEditorVisible || SelectedSecurityGuard is null) return;
        SecurityGuards.Remove(SelectedSecurityGuard);
        SelectedSecurityGuard = null;
        RefreshSecurityMapCollections();
        SecurityRevision++;
    }

    [RelayCommand]
    private void RemoveSelectedPatrol()
    {
        if (!IsSecurityEditorVisible || SelectedSecurityPatrol is null) return;
        var id = SelectedSecurityPatrol.Id;
        SecurityPatrols.Remove(SelectedSecurityPatrol);
        foreach (var guard in SecurityGuards) guard.PatrolIds.Remove(id);
        SelectedSecurityPatrol = null;
        RefreshSecurityMapCollections();
        SecurityRevision++;
    }

    [RelayCommand]
    private void SaveSecurityDataset()
    {
        if (!IsSecurityEditorVisible) return;
        if (SelectedSecurityGuard is not null)
            OnSelectedSecurityGuardChanged(SelectedSecurityGuard, SelectedSecurityGuard);
        try
        {
            _securityDatasetStore.Save(new(
                SecurityCameras.Select(item => item.ToDomain()).ToList(),
                SecurityGuards.Select(item => item.ToDomain()).ToList(),
                SecurityPatrols.Select(item => item.ToDomain()).ToList()));
            SecurityStatus = "Security dataset saved.";
        }
        catch (InvalidDataException exception) { SecurityStatus = exception.Message; }
    }

    [RelayCommand]
    private void SelectPatrolWaypoint(SecurityWaypointSelection selection)
    {
        if (!IsSecurityEditorVisible) return;
        SelectedSecurityPatrol = SecurityPatrols.FirstOrDefault(item => item.Id == selection.PatrolId);
        SelectedPatrolWaypointIndex = SelectedSecurityPatrol is not null ? selection.Index : -1;
        SecurityRevision++;
    }

    [RelayCommand]
    private void MovePatrolWaypoint(SecurityWaypointMove move)
    {
        if (!IsSecurityEditorVisible) return;
        var patrol = SecurityPatrols.FirstOrDefault(item => item.Id == move.PatrolId);
        if (patrol is null) return;
        var updated = PatrolRouteEditor.Move(patrol.Waypoints, move.Index, new(move.X, move.Y));
        patrol.Waypoints.Clear();
        foreach (var waypoint in updated) patrol.Waypoints.Add(waypoint);
        SelectedSecurityPatrol = patrol;
        SelectedPatrolWaypointIndex = move.Index;
        SecurityRevision++;
        SecurityStatus = "Unsaved patrol waypoint change.";
    }

    [RelayCommand]
    private void DeleteSelectedPatrolWaypoint()
    {
        if (!IsSecurityEditorVisible || SelectedSecurityPatrol is null || SelectedPatrolWaypointIndex < 0) return;
        var updated = PatrolRouteEditor.Delete(SelectedSecurityPatrol.Waypoints, SelectedPatrolWaypointIndex);
        SelectedSecurityPatrol.Waypoints.Clear();
        foreach (var waypoint in updated) SelectedSecurityPatrol.Waypoints.Add(waypoint);
        SelectedPatrolWaypointIndex = -1;
        SecurityRevision++;
        SecurityStatus = "Patrol waypoint deleted; adjacent segments reconnected. Save to persist.";
    }

    [RelayCommand]
    private void SetVaultCode(string? code) => VaultCode = string.IsNullOrWhiteSpace(code) ? null : code.Trim();

    [RelayCommand]
    private void FocusMap(string mapId)
    {
        var card = VisibleMapCards.FirstOrDefault(item => item.Map.Id == mapId);
        if (card is null) return;
        SelectedMap = card.Map;
        FocusedMapId = mapId;
    }

    [RelayCommand]
    private void ExitMapFocus() => FocusedMapId = null;

    private void OnSecurityObjectChanged(object? sender, PropertyChangedEventArgs e)
    {
        SecurityRevision++;
        SecurityStatus = "Unsaved security changes.";
    }

    private void RefreshSecurityMapCollections()
    {
        OnPropertyChanged(nameof(CurrentMapCameras));
        OnPropertyChanged(nameof(CurrentMapGuards));
        OnPropertyChanged(nameof(CurrentMapPatrols));
    }

    private string NextSecurityId(string kind, IEnumerable<string> existing)
    {
        var ids = existing.ToHashSet(StringComparer.Ordinal);
        var sequence = 1;
        string id;
        do id = $"{SelectedMap.Id}-{kind}-{sequence++:00}"; while (ids.Contains(id));
        return id;
    }

    private void ApplyStagePolicy()
    {
        var policy = CurrentPolicy;
        var selectedId = SelectedMap?.Id;
        FocusedMapId = null;
        Maps.Clear();
        foreach (var card in VisibleMapCards) card.Dispose();
        VisibleMapCards.Clear();
        foreach (var map in KortzMapCatalog.Maps.Where(map => policy.AllowsMap(map.Id)))
        {
            Maps.Add(map);
            VisibleMapCards.Add(new MapCardViewModel(this, map));
        }

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
        _scopeOutSession = new ScopeOutVoiceSession(_lootRun.Definitions, _lootRun);
        _scopeOutSession.SetListening(MicrophoneStatus != MicrophoneStatus.Off);
        OnPropertyChanged(nameof(ScopeOutState));
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
        return _initialCalibration;
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

    public void Dispose()
    {
        _inputDetectedResetTimer.Stop();
        _speechRecognitionService.SpeechRecognized -= OnSpeechRecognized;
        _speechRecognitionService.PartialTranscriptChanged -= OnPartialTranscriptChanged;
        _speechRecognitionService.RecognitionFailed -= OnRecognitionFailed;
        _audioCaptureService.FrameCaptured -= OnAudioFrameCaptured;
        _speechRecognitionService.Dispose();
        _audioCaptureService.Dispose();
    }
}
