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
    private readonly HeistSessionStore _heistSessionStore;
    private readonly MapCalibration _initialCalibration;
    private ApplicationSettings? _settingsSnapshot;
    private LootRunState _lootRun = new([]);
    private bool _updatingCalibrationFields;
    private readonly ISpeechRecognitionService _speechRecognitionService;
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly DispatcherTimer _inputDetectedResetTimer;
    private readonly DispatcherTimer _voiceUiHeartbeatTimer;
    private readonly DispatcherTimer _voiceFeedbackTimer;
    private int _audioUiUpdatePending;
    private ScopeOutVoiceSession? _scopeOutSession;
    private VoiceCommandParser _voiceCommandParser = new([]);
    private readonly VoiceCommandContext _voiceContext = new();
    private readonly Stack<(string Description, Action Undo)> _voiceUndo = new();
    private readonly Dictionary<string, CameraDisableMethod> _disabledCameras = new(StringComparer.Ordinal);
    private string? _lastUndoneVoiceAction;
    private Guid _heistId = Guid.NewGuid();
    private DateTimeOffset _heistCreatedAtUtc = DateTimeOffset.UtcNow;

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
    [NotifyPropertyChangedFor(nameof(IsInfiltrationStage))]
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
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GuardsDownDisplay))]
    public partial int GuardsDown { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CamerasDownDisplay))]
    [NotifyPropertyChangedFor(nameof(CameraStatusColor))]
    [NotifyPropertyChangedFor(nameof(IsCameraStealthCompromised))]
    [NotifyPropertyChangedFor(nameof(TotalDisabledCameras))]
    public partial int CamerasDown { get; set; }
    [ObservableProperty] public partial string VoiceRecognitionState { get; set; } = "Stopped";
    [ObservableProperty] public partial string? LastRecognizedText { get; set; }
    [ObservableProperty] public partial string? LastParsedVoiceCommand { get; set; }
    [ObservableProperty] public partial string? VoiceCommandError { get; set; }
    [ObservableProperty] public partial string? LastVoiceLootUpdate { get; set; }
    [ObservableProperty] public partial string VoiceSimulatorText { get; set; } = string.Empty;
    [ObservableProperty] public partial string? VoiceStartupDiagnostics { get; set; }
    [ObservableProperty] public partial string? CurrentPartialTranscript { get; set; }
    [ObservableProperty] public partial DateTimeOffset? VoiceUiHeartbeat { get; set; }
    [ObservableProperty] public partial bool IsNewHeistConfirmationOpen { get; set; }
    [ObservableProperty] public partial bool IsEndHeistConfirmationOpen { get; set; }
    [ObservableProperty] public partial bool IsLoadHeistConfirmationOpen { get; set; }
    [ObservableProperty] public partial bool IsVoiceGuideOpen { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVoiceFeedback))]
    public partial VoiceFeedback? CurrentVoiceFeedback { get; set; }
    private string? _pendingLoadHeistPath;
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
        new(PlannerStage.HeistActivity, "Heist"),
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
    public bool IsInfiltrationStage => CurrentStage == PlannerStage.HeistInfiltration;
    public bool IsPreparationStage => CurrentStage == PlannerStage.Preparation;
    public bool HasSelectedPatrolWaypoint => SelectedSecurityPatrol is not null && SelectedPatrolWaypointIndex >= 0;
    public bool HasFocusedMap => FocusedMapId is not null;
    public MapCardViewModel? FocusedMapCard => VisibleMapCards.FirstOrDefault(card => card.Map.Id == FocusedMapId);
    public bool IsSewerSelected => SelectedMap.Id == "sewer";
    public LootPlanningSummary PlanningSummary => LootPlanningSummaryCalculator.Calculate(
        LootMarkers.Select(marker => marker.ToDefinition()), _lootRun.States, PlayerCount);
    public PlanningAnalysis PlanningAnalysis => HeistPlanningAnalyzer.Analyze(
        LootMarkers.Select(marker => marker.ToDefinition()), _lootRun.States, PlayerCount);
    public string PlanningValueRange => $"${PlanningSummary.EstimatedMinValue:N0} – ${PlanningSummary.EstimatedMaxValue:N0}";
    public string PlanningKnownValue => $"${PlanningAnalysis.KnownExactValue:N0}";
    public string PlanningEstimatedRange => $"${PlanningAnalysis.EstimatedMinValue:N0}–${PlanningAnalysis.EstimatedMaxValue:N0}";
    public string PlanningPotentialRange => $"${PlanningAnalysis.PotentialMinValue:N0}–${PlanningAnalysis.PotentialMaxValue:N0}";
    public string RecommendedHaulRange => $"${PlanningAnalysis.RecommendedHaul.TotalMinPotentialValue:N0}–${PlanningAnalysis.RecommendedHaul.TotalMaxPotentialValue:N0}";
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
    public ScopeOutSessionState ScopeOutState => MicrophoneStatus == MicrophoneStatus.Off
        ? ScopeOutSessionState.Inactive
        : _voiceContext.ScopeOutActive ? ScopeOutSessionState.Active : ScopeOutSessionState.WaitingForActivation;

    public string ConfiguredCaptureDevice => SelectedRecordingDevice is null
        ? "No recording device selected"
        : $"{SelectedRecordingDevice.Name} ({SelectedRecordingDevice.Id})";
    public string TransmittedAudioFormat => "24,000 Hz, 16-bit mono PCM";
    public string GuardsDownDisplay => $"Guards down: {GuardsDown}";
    public int CountedCameraTakedowns => CamerasDown;
    public int TotalDisabledCameras => SecurityCameras.Count(camera => !camera.IsActive) +
        Math.Max(0, CamerasDown - _disabledCameras.Count(entry => entry.Value == CameraDisableMethod.Destroyed));
    public bool IsCameraStealthCompromised => CamerasDown > HeistSessionState.CameraDisableLimit;
    public string CamerasDownDisplay => IsCameraStealthCompromised
        ? $"Cameras disabled: {TotalDisabledCameras} · Takedowns: {CamerasDown} counted · STEALTH BROKEN"
        : $"Cameras disabled: {TotalDisabledCameras} · Takedowns: {CamerasDown} / {HeistSessionState.CameraDisableLimit}";
    public string CameraStatusColor => CamerasDown switch { 0 => "#63E67A", <= 2 => "#E8A65A", _ => "#FF7777" };
    public string VoiceContextSummary => $"Last loot: {_voiceContext.LastMentionedLootId ?? "—"}; pending: {_voiceContext.PendingMode}; focus: {FocusedMapId ?? "overview"}; undo: {_voiceUndo.Count} ({_voiceContext.LastReversibleAction ?? "—"})";
    public string ActivityLootSummary
    {
        get
        {
            var collected = LootMarkers.Where(marker => marker.IsLooted).ToArray();
            return $"Secondary loot: {collected.Length} collected · ${collected.Sum(marker => marker.ScopedValue ?? 0):N0} known · {collected.Sum(marker => marker.Economics.BagPercent)}% bag";
        }
    }
    public string HeistSavePath => _heistSessionStore.FilePath;
    public bool HasVoiceFeedback => CurrentVoiceFeedback is not null;
    public IReadOnlyList<VoiceGuideGroup> CurrentVoiceGuideGroups => VoiceCommandCatalog.Definitions
        .Where(definition => definition.Stages.Contains(CurrentStage))
        .GroupBy(definition => definition.Category)
        .Select(group => new VoiceGuideGroup(group.Key, group.SelectMany(definition => definition.Aliases).Distinct().ToArray()))
        .ToArray();
    public string VoiceHint => CurrentStage switch
    {
        PlannerStage.Preparation => "🎙 Scope out  •  <loot name>  •  <value>  •  Buyer's request",
        PlannerStage.Planning => "🎙 Plan out  •  Pull up <map>  •  Pull back  •  Undo",
        PlannerStage.HeistInfiltration => "🎙 Tango down  •  Camera down  •  Pull up rooftop  •  Going down skylight",
        PlannerStage.HeistActivity => "🎙 <loot name>  •  Pull up <map>  •  Pull back  •  Undo",
        _ => string.Empty,
    };

    public MainViewModel() : this(
        new OpenAiRealtimeSpeechRecognitionService(diagnostics: new VoiceDiagnosticTrace(() => Dispatcher.UIThread.CheckAccess())),
        new WasapiAudioCaptureService(new VoiceDiagnosticTrace(() => Dispatcher.UIThread.CheckAccess())))
    {
    }

    public MainViewModel(
        ISpeechRecognitionService speechRecognitionService,
        IAudioCaptureService audioCaptureService,
        HeistSessionStore? heistSessionStore = null)
    {
        _speechRecognitionService = speechRecognitionService;
        _audioCaptureService = audioCaptureService;
        _heistSessionStore = heistSessionStore ?? new HeistSessionStore();
        _inputDetectedResetTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _inputDetectedResetTimer.Tick += (_, _) =>
        {
            _inputDetectedResetTimer.Stop();
            SetMicrophoneInputDetected(false);
        };
        _voiceUiHeartbeatTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _voiceUiHeartbeatTimer.Tick += (_, _) =>
        {
            VoiceUiHeartbeat = DateTimeOffset.Now;
            _speechRecognitionService.Diagnostics.RecordMilestone("Developer UI heartbeat");
            RefreshVoiceStartupDiagnostics();
        };
        _voiceFeedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2500) };
        _voiceFeedbackTimer.Tick += (_, _) =>
        {
            _voiceFeedbackTimer.Stop();
            CurrentVoiceFeedback = null;
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
        LoadCurrentHeistOnStartup();
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
        _voiceContext.ClearNumericContinuation();
        if (value != PlannerStage.Preparation && ScopeOutState == ScopeOutSessionState.Active)
        {
            _scopeOutSession?.SetListening(MicrophoneStatus != MicrophoneStatus.Off);
            _voiceContext.ScopeOutActive = false;
            _voiceContext.PendingLootValueTargetId = null;
            OnPropertyChanged(nameof(ScopeOutState));
        }
        ApplyStagePolicy();
        SelectedLoot = null;
        HoveredMarker = null;
        OnPropertyChanged(nameof(ShowLootOverlay));
        OnPropertyChanged(nameof(IsLootAuthoringEnabled));
        OnPropertyChanged(nameof(CurrentVoiceGuideGroups));
        OnPropertyChanged(nameof(VoiceHint));
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
            _voiceUiHeartbeatTimer.Stop();
        }
        else if (MicrophoneStatus != MicrophoneStatus.Off)
            _voiceUiHeartbeatTimer.Start();
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
            ObserveVoiceTask(StopVoiceRecognitionAsync(), "Disable microphone");
    }

    [RelayCommand]
    private async Task ToggleMicrophone()
    {
        _speechRecognitionService.Diagnostics.RecordMilestone("Mic toggle entered");
        if (!MicrophoneEnabled)
            return;
        if (MicrophoneStatus == MicrophoneStatus.Off)
            await StartVoiceRecognitionAsync();
        else
            await StopVoiceRecognitionAsync();
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
                .Concat(KortzMapCatalog.Maps.Select(map => map.DisplayName))
                .Concat(SecurityCameras.SelectMany(camera => new[] { $"{camera.Name} down", $"{camera.Name} disabled" }))
                .Concat(SewerConnections.SelectMany(connection => SewerVocabulary(connection)))
                .Concat(["scope out", "stop scope out", "special loot", "buyer's request", "vault code",
                    "plan out", "plan it", "pan out", "pan it", "overview", "heist start", "start heist",
                    "start infiltration", "infiltration start", "tango down", "dropped guard", "camera down",
                    "charlie down", "going down skylight", "using access codes", "alpha mail arriving",
                    "sewer grate reached", "sewer route", "shot the button", "chamber", "alpha", "bravo", "charlie", "delta", "echo",
                    "outta the sewers", "pull up", "pull back", "back up", "undo",
                    "painting", "rings", "loading bay cargo", "safety deposit boxes", "Glass Cutter", "Power Drills", "Coquard"])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            await _speechRecognitionService.StartAsync(_audioCaptureService.Format, keywords);
            MicrophoneStatus = MicrophoneStatus.Listening;
            _scopeOutSession?.SetListening(true);
            VoiceRecognitionState = _speechRecognitionService.StateDescription;
            RefreshVoiceStartupDiagnostics();
            VoiceCommandError = null;
            if (DeveloperMode)
                _voiceUiHeartbeatTimer.Start();
            _speechRecognitionService.Diagnostics.RecordMilestone("UI status updated after microphone startup");
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

    private async Task StopVoiceRecognitionAsync()
    {
        _speechRecognitionService.Diagnostics.RecordMilestone("UI microphone stop entered");
        _inputDetectedResetTimer.Stop();
        _voiceUiHeartbeatTimer.Stop();
        _audioCaptureService.Stop();
        await _speechRecognitionService.StopAsync();
        MicrophoneStatus = MicrophoneStatus.Off;
        _scopeOutSession?.SetListening(false);
        _voiceContext.ScopeOutActive = false;
        _voiceContext.PendingLootValueTargetId = null;
        _voiceContext.ClearNumericContinuation();
        VoiceRecognitionState = _speechRecognitionService.StateDescription;
        OnPropertyChanged(nameof(ScopeOutState));
        RefreshVoiceContextDiagnostics();
        _speechRecognitionService.Diagnostics.RecordMilestone("UI microphone stop completed");
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
            _speechRecognitionService.Diagnostics.RecordMilestone("Transcript UI callback");
            CurrentPartialTranscript = null;
            ProcessRecognizedText(e.Text);
        });

    private void OnPartialTranscriptChanged(object? sender, PartialTranscriptEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            _speechRecognitionService.Diagnostics.RecordMilestone("Partial transcript UI status update");
            CurrentPartialTranscript = e.Text;
            VoiceRecognitionState = _speechRecognitionService.StateDescription;
            RefreshVoiceStartupDiagnostics();
        });

    private void OnRecognitionFailed(object? sender, SpeechRecognitionFailedEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            _speechRecognitionService.Diagnostics.RecordMilestone("Recognition failure UI status update");
            VoiceCommandError = e.Exception.ToString();
            VoiceRecognitionState = "Recognition unavailable";
            RefreshVoiceStartupDiagnostics();
            ObserveVoiceTask(StopVoiceRecognitionAsync(), "Recognition failure cleanup");
        });

    private void OnAudioFrameCaptured(object? sender, PcmAudioFrameEventArgs e)
    {
        _speechRecognitionService.PushAudio(e.Data, e.ActivityLevel);
        if (Interlocked.Exchange(ref _audioUiUpdatePending, 1) != 0)
            return;
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                HandleAudioLevel(e.ActivityLevel);
                RefreshVoiceStartupDiagnostics();
            }
            finally { Interlocked.Exchange(ref _audioUiUpdatePending, 0); }
        });
    }

    private void ObserveVoiceTask(Task task, string operation)
    {
        _ = ObserveVoiceTaskCoreAsync(task, operation);
    }

    private async Task ObserveVoiceTaskCoreAsync(Task task, string operation)
    {
        try { await task; }
        catch (Exception exception)
        {
            _speechRecognitionService.Diagnostics.RecordException(operation, exception);
            VoiceCommandError = exception.ToString();
            RefreshVoiceStartupDiagnostics();
        }
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

        if (MicrophoneStatus == MicrophoneStatus.Off)
        {
            VoiceCommandError = "Microphone is off.";
            AddVoiceTranscript(text, false);
            return;
        }
        var parse = _voiceCommandParser.Parse(text, _voiceContext, CurrentStage);
        LastParsedVoiceCommand = parse.Command?.GetType().Name ?? parse.Disposition.ToString();
        VoiceCommandError = parse.Error;
        if (parse.Command is not null and not (RecordScopedLootCommand or SetPendingLootValueCommand or ContinueLootValueCommand))
            _voiceContext.ClearNumericContinuation();
        var applied = parse.Command is not null && ExecuteVoiceCommand(parse.Command);
        if (applied)
            ShowVoiceFeedback(VoiceFeedbackKind.Success, SuccessMessage(parse.Command!));
        else if (parse.Command is not null)
            ShowVoiceFeedback(VoiceFeedbackKind.Rejected, $"! {VoiceCommandError ?? "Command rejected"}");
        else
            ShowVoiceFeedback(VoiceFeedbackKind.NotRecognized, $"? Not recognized: \"{text.Trim()}\"");
        AddVoiceTranscript(text, applied);
        RefreshVoiceContextDiagnostics();
    }

    private bool ExecuteVoiceCommand(VoiceCommand command)
    {
        switch (command)
        {
            case ActivateScopeOutCommand:
                CurrentStage = PlannerStage.Preparation;
                _voiceContext.ScopeOutActive = true;
                VoiceCommandError = null;
                return true;
            case DeactivateScopeOutCommand:
                _voiceContext.ScopeOutActive = false;
                _voiceContext.PendingLootValueTargetId = null;
                return true;
            case RecordScopedLootCommand loot:
                return ApplyVoiceLoot(loot);
            case SetPendingLootValueCommand value:
                return ApplyPendingLootValue(value);
            case ContinueLootValueCommand continuation:
                return ApplyLootValueContinuation(continuation);
            case ToggleSpecialLootCommand:
                return ToggleLastMentionedSpecialLoot();
            case UndoVoiceCommand:
                return UndoLastVoiceAction();
            case FocusMapVoiceCommand focus:
                if (!CurrentPolicy.AllowsMap(focus.MapId)) return RejectVoice("That map is not available in the current stage.");
                FocusMap(focus.MapId);
                return true;
            case ExitMapFocusVoiceCommand:
                ExitMapFocus();
                return true;
            case SetVaultCodeVoiceCommand vault:
                if (vault.VaultCode is null) { _voiceContext.AwaitingVaultCode = true; return true; }
                var oldCode = VaultCode;
                VaultCode = vault.VaultCode;
                _voiceContext.AwaitingVaultCode = false;
                PushVoiceUndo($"vault code {vault.VaultCode}", () => VaultCode = oldCode);
                return true;
            case ChangeStageVoiceCommand stage:
                if (stage.Stage == PlannerStage.HeistActivity && CurrentStage != PlannerStage.HeistInfiltration)
                    return RejectVoice("An infiltration entry must be active before entering Heist.");
                var oldStage = CurrentStage;
                var showroomSnapshot = SnapshotCamera(KortzSecurityIds.ShowroomCamera);
                CurrentStage = stage.Stage;
                if (stage.IsSkylightEntry)
                    DisableCamera(KortzSecurityIds.ShowroomCamera, CameraDisableMethod.InfiltrationAutoDisable, false);
                PushVoiceUndo($"stage changed to {StageOptions.First(option => option.Stage == stage.Stage).Label}", () =>
                {
                    CurrentStage = oldStage;
                    RestoreCamera(KortzSecurityIds.ShowroomCamera, showroomSnapshot);
                });
                return true;
            case IncrementGuardsDownVoiceCommand:
                if (CurrentStage != PlannerStage.HeistInfiltration) return RejectVoice("Guard counters are available during Infiltration.");
                var oldGuards = GuardsDown;
                GuardsDown++;
                PushVoiceUndo("guard down", () => GuardsDown = oldGuards);
                return true;
            case IncrementCamerasDownVoiceCommand:
                if (CurrentStage != PlannerStage.HeistInfiltration) return RejectVoice("Camera counters are available during Infiltration.");
                var oldCameras = CamerasDown;
                CamerasDown++;
                PushVoiceUndo("camera down", () => CamerasDown = oldCameras);
                return true;
            case DisableNamedCameraVoiceCommand namedCamera:
                if (CurrentStage != PlannerStage.HeistInfiltration) return RejectVoice("Camera takedowns are available during Infiltration.");
                return ApplyNamedCameraDisable(namedCamera.CameraId, CameraDisableMethod.Destroyed, true);
            case DisableShowroomByButtonVoiceCommand:
                if (CurrentStage != PlannerStage.HeistInfiltration) return RejectVoice("The showroom disable button is available during Infiltration.");
                return ApplyNamedCameraDisable(KortzSecurityIds.ShowroomCamera, CameraDisableMethod.DisableButton, false);
            case EnterSewerRouteVoiceCommand:
                CurrentStage = PlannerStage.HeistInfiltration;
                FocusMap("sewer");
                _voiceContext.AwaitingSewerRoute = true;
                return true;
            case ApplySewerRouteVoiceCommand route:
                try
                {
                    CurrentStage = PlannerStage.HeistInfiltration;
                    FocusMap("sewer");
                    SewerRouteInput = route.RouteText;
                    SetSewerRoute(SewerRouteParser.Parse(route.RouteText));
                    if (!IsSewerRouteComplete) return RejectVoice($"Parsed: {route.RouteText}. {SewerDiagnostic ?? "Sewer route is incomplete."}");
                    _voiceContext.AwaitingSewerRoute = false;
                    SewerDiagnostic = $"Parsed: {route.RouteText}. {SewerDiagnostic}";
                    return true;
                }
                catch (Exception exception) when (exception is FormatException or InvalidDataException)
                {
                    return RejectVoice(exception.Message);
                }
            case ExitSewerRouteVoiceCommand:
                _voiceContext.AwaitingSewerRoute = false;
                CurrentStage = PlannerStage.HeistActivity;
                FocusedMapId = null;
                return true;
            default:
                return RejectVoice("Unsupported voice command.");
        }
    }

    private static IEnumerable<string> SewerVocabulary(SewerConnection connection)
    {
        yield return $"{connection.ChamberA} {connection.TunnelFromA}";
        if (connection.ChamberB is { } chamber && connection.TunnelFromB is { } tunnel)
            yield return $"{chamber} {tunnel}";
    }

    private bool ApplyNamedCameraDisable(string cameraId, CameraDisableMethod method, bool countsAgainstStealth)
    {
        var camera = SecurityCameras.FirstOrDefault(item => item.Id == cameraId);
        if (camera is null) return RejectVoice($"Camera '{cameraId}' is not present in the current security dataset.");
        if (!camera.IsActive) return true;
        var snapshot = SnapshotCamera(cameraId);
        DisableCamera(cameraId, method, countsAgainstStealth);
        PushVoiceUndo($"{camera.Name} disabled", () => RestoreCamera(cameraId, snapshot));
        return true;
    }

    private void DisableCamera(string cameraId, CameraDisableMethod method, bool countsAgainstStealth)
    {
        var camera = SecurityCameras.FirstOrDefault(item => item.Id == cameraId)
            ?? throw new InvalidOperationException($"Camera '{cameraId}' is not present in the current security dataset.");
        if (!camera.IsActive) return;
        camera.IsActive = false;
        _disabledCameras[cameraId] = method;
        if (countsAgainstStealth) CamerasDown++;
        NotifyCameraRuntimeChanged();
    }

    private (bool IsActive, CameraDisableMethod? Method, int Counted) SnapshotCamera(string cameraId) =>
        (SecurityCameras.FirstOrDefault(item => item.Id == cameraId)?.IsActive ?? true,
            _disabledCameras.GetValueOrDefault(cameraId), CamerasDown);

    private void RestoreCamera(string cameraId, (bool IsActive, CameraDisableMethod? Method, int Counted) snapshot)
    {
        if (SecurityCameras.FirstOrDefault(item => item.Id == cameraId) is { } camera) camera.IsActive = snapshot.IsActive;
        if (snapshot.Method is { } method) _disabledCameras[cameraId] = method;
        else _disabledCameras.Remove(cameraId);
        CamerasDown = snapshot.Counted;
        NotifyCameraRuntimeChanged();
    }

    private void NotifyCameraRuntimeChanged()
    {
        SecurityRevision++;
        OnPropertyChanged(nameof(TotalDisabledCameras));
        OnPropertyChanged(nameof(CamerasDownDisplay));
    }

    private bool ApplyVoiceLoot(RecordScopedLootCommand command)
    {
        _voiceContext.ClearNumericContinuation();
        var state = _lootRun.GetState(command.LootLocationId);
        var before = Snapshot(state);
        if (CurrentStage == PlannerStage.HeistActivity)
            _lootRun.SetLooted(command.LootLocationId, true);
        else
            _lootRun.RecordScopedLoot(command.LootLocationId, command.ScopedValue);
        _voiceContext.LastMentionedLootId = command.LootLocationId;
        _voiceContext.PendingLootValueTargetId = CurrentStage == PlannerStage.Preparation && command.ScopedValue is null ? command.LootLocationId : null;
        if (CurrentStage == PlannerStage.Preparation && command.ScopedValue is { } value && IsContinuationEligible(command.ValueParse))
        {
            _voiceContext.NumericContinuationTargetId = command.LootLocationId;
            _voiceContext.NumericContinuationBaseValue = value;
        }
        PushVoiceUndo(CurrentStage == PlannerStage.HeistActivity ? "loot collected" : "loot scoped", () => RestoreLoot(command.LootLocationId, before));
        RefreshVoiceLoot(command.LootLocationId);
        return true;
    }

    private bool ApplyPendingLootValue(SetPendingLootValueCommand command)
    {
        var id = _voiceContext.PendingLootValueTargetId;
        if (!_voiceContext.ScopeOutActive || id is null) return RejectVoice("No loot target is awaiting a value.");
        var state = _lootRun.GetState(id);
        var before = Snapshot(state);
        _lootRun.RecordScopedLoot(id, command.ScopedValue);
        _voiceContext.PendingLootValueTargetId = null;
        _voiceContext.ClearNumericContinuation();
        if (IsContinuationEligible(command.ValueParse))
        {
            _voiceContext.NumericContinuationTargetId = id;
            _voiceContext.NumericContinuationBaseValue = command.ScopedValue;
        }
        PushVoiceUndo($"loot value ${command.ScopedValue:N0}", () => RestoreLoot(id, before));
        RefreshVoiceLoot(id);
        return true;
    }

    private bool ApplyLootValueContinuation(ContinueLootValueCommand command)
    {
        if (!_voiceContext.ScopeOutActive || _voiceContext.NumericContinuationTargetId != command.LootLocationId ||
            _voiceContext.NumericContinuationBaseValue is not { } baseValue)
            return RejectVoice("No loot value is awaiting a numeric continuation.");
        var combined = checked(baseValue + command.Remainder);
        _lootRun.RecordScopedLoot(command.LootLocationId, combined);
        _speechRecognitionService.Diagnostics.Record($"Numeric continuation: ${baseValue:N0} + ${command.Remainder:N0} -> ${combined:N0}");
        RefreshVoiceStartupDiagnostics();
        _voiceContext.ClearNumericContinuation();
        RefreshVoiceLoot(command.LootLocationId);
        return true;
    }

    private static bool IsContinuationEligible(SpokenNumberResult? result) =>
        result is { UsedThousandsUnit: true, HasExplicitSubThousandComponent: false };

    private bool ToggleLastMentionedSpecialLoot()
    {
        var id = _voiceContext.LastMentionedLootId;
        if (id is null) return RejectVoice("No previously mentioned loot target is available for Buyer's Request.");
        var state = _lootRun.GetState(id);
        var before = Snapshot(state);
        if (!_lootRun.TrySetBuyersRequest(id, !state.IsBuyersRequest, out var error)) return RejectVoice(error!);
        PushVoiceUndo("Buyer's Request toggled", () => RestoreLoot(id, before));
        RefreshVoiceLoot(id);
        return true;
    }

    private bool UndoLastVoiceAction()
    {
        if (!_voiceUndo.TryPop(out var action)) return RejectVoice("There is no reversible voice action to undo.");
        action.Undo();
        _lastUndoneVoiceAction = action.Description;
        _voiceContext.LastReversibleAction = _voiceUndo.TryPeek(out var next) ? next.Description : null;
        _speechRecognitionService.Diagnostics.Record($"Voice undo: {action.Description}");
        RefreshLootState();
        RefreshVoiceContextDiagnostics();
        return true;
    }

    private void PushVoiceUndo(string description, Action undo)
    {
        _voiceUndo.Push((description, undo));
        _voiceContext.LastReversibleAction = description;
    }

    private static (bool Present, bool Buyers, bool Looted, int? Value) Snapshot(LootSpawnState state) =>
        (state.IsPresent, state.IsBuyersRequest, state.IsLooted, state.ScopedValue);

    private void RestoreLoot(string id, (bool Present, bool Buyers, bool Looted, int? Value) value)
    {
        var state = _lootRun.GetState(id);
        state.IsPresent = value.Present; state.IsBuyersRequest = value.Buyers; state.IsLooted = value.Looted; state.ScopedValue = value.Value;
        RefreshVoiceLoot(id);
    }

    private void RefreshVoiceLoot(string id)
    {
        var marker = LootMarkers.Single(item => item.Id == id);
        marker.ApplyState(_lootRun.GetState(id));
        SelectedLoot = marker;
        LootRevision++;
        LastVoiceLootUpdate = $"{marker.Name}: present={marker.IsPresent}, value={marker.ScopedValue?.ToString() ?? "—"}, special={marker.IsBuyersRequest}, looted={marker.IsLooted}";
        RefreshPlanningSummary();
        OnPropertyChanged(nameof(ActivityLootSummary));
        OnPropertyChanged(nameof(BuyersRequestStatus));
    }

    private bool RejectVoice(string error) { VoiceCommandError = error; return false; }
    private void ShowVoiceFeedback(VoiceFeedbackKind kind, string message)
    {
        CurrentVoiceFeedback = new(kind, message);
        _voiceFeedbackTimer.Stop();
        _voiceFeedbackTimer.Start();
    }

    private string SuccessMessage(VoiceCommand command) => command switch
    {
        SetPendingLootValueCommand value => $"✓ Loot value · ${value.ScopedValue:N0}",
        ContinueLootValueCommand continuation => $"✓ {LootMarkers.First(marker => marker.Id == continuation.LootLocationId).Name} · ${_lootRun.GetState(continuation.LootLocationId).ScopedValue:N0}",
        DisableNamedCameraVoiceCommand named => CameraAcknowledgement(SecurityCameras.First(camera => camera.Id == named.CameraId).Name),
        DisableShowroomByButtonVoiceCommand => "✓ Showroom Camera disabled · does not count against stealth",
        ChangeStageVoiceCommand { IsSkylightEntry: true } => "✓ Skylight infiltration · Showroom Camera automatically disabled",
        IncrementCamerasDownVoiceCommand => CameraAcknowledgement("Camera"),
        _ => BaseSuccessMessage(command),
    };

    private string CameraAcknowledgement(string name) => IsCameraStealthCompromised
        ? $"! {name} disabled · {CamerasDown} counted · Stealth broken"
        : $"✓ {name} disabled · {CamerasDown} / {HeistSessionState.CameraDisableLimit}";

    private string BaseSuccessMessage(VoiceCommand command) => command switch
    {
        IncrementGuardsDownVoiceCommand => $"✓ Guard down · {GuardsDown}",
        IncrementCamerasDownVoiceCommand => $"✓ Camera down · {CamerasDown} / {HeistSessionState.CameraDisableLimit}",
        FocusMapVoiceCommand focus => $"✓ {KortzMapCatalog.Maps.First(map => map.Id == focus.MapId).DisplayName} focused",
        ExitMapFocusVoiceCommand => "✓ Map overview",
        ChangeStageVoiceCommand stage => $"✓ {StageOptions.First(option => option.Stage == stage.Stage).Label}",
        RecordScopedLootCommand loot => $"✓ {LootMarkers.First(marker => marker.Id == loot.LootLocationId).Name} · {(CurrentStage == PlannerStage.HeistActivity ? "looted" : "scoped")}",
        UndoVoiceCommand => $"✓ Undid: {_lastUndoneVoiceAction ?? "last action"}",
        EnterSewerRouteVoiceCommand => "✓ Sewer route ready",
        ApplySewerRouteVoiceCommand => "✓ Sewer route applied",
        _ => $"✓ {VoiceCommandCatalog.Definitions.FirstOrDefault(definition => definition.Aliases.Any(alias => command.GetType().Name.StartsWith(definition.Name.Replace(" ", ""), StringComparison.OrdinalIgnoreCase)))?.Name ?? "Command applied"}",
    };

    [RelayCommand] private void OpenVoiceGuide() => IsVoiceGuideOpen = true;
    [RelayCommand] private void CloseVoiceGuide() => IsVoiceGuideOpen = false;
    private void RefreshVoiceContextDiagnostics()
    {
        OnPropertyChanged(nameof(ScopeOutState));
        OnPropertyChanged(nameof(VoiceContextSummary));
        OnPropertyChanged(nameof(ActivityLootSummary));
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
        CamerasDown = 0;
        _disabledCameras.Clear();
        NotifyCameraRuntimeChanged();
        RefreshLootState();
        LootStatus = "Heist loot state reset; permanent layout unchanged.";
    }

    [RelayCommand]
    private void NewHeist()
    {
        IsNewHeistConfirmationOpen = true;
    }

    [RelayCommand]
    private void CancelNewHeist() => IsNewHeistConfirmationOpen = false;

    [RelayCommand]
    private void ConfirmNewHeist()
    {
        IsNewHeistConfirmationOpen = false;
        GuardsDown = 0;
        CamerasDown = 0;
        _disabledCameras.Clear();
        FocusedMapId = null;
        SewerRouteInput = string.Empty;
        HighlightedSewerPathIds.Clear();
        IsSewerRouteComplete = false;
        SewerDiagnostic = null;
        _voiceContext.Reset();
        _voiceUndo.Clear();
        CurrentStage = PlannerStage.HeistInfiltration;
        foreach (var state in _lootRun.States) state.IsLooted = false;
        foreach (var camera in SecurityCameras) camera.IsActive = true;
        foreach (var guard in SecurityGuards) guard.IsActive = true;
        foreach (var patrol in SecurityPatrols) patrol.IsActive = true;
        RefreshLootState();
        RefreshVoiceContextDiagnostics();
        SaveHeistCore();
        SaveStatus = "New attempt started. Preparation and planning data were preserved.";
    }

    [RelayCommand]
    private void SaveHeist()
    {
        try
        {
            SaveHeistCore();
            SaveStatus = $"Heist saved to {_heistSessionStore.FilePath}";
        }
        catch (Exception exception)
        {
            SaveStatus = $"Could not save heist: {exception.Message}";
        }
    }

    [RelayCommand]
    private void LoadHeist(string? sourcePath)
    {
        _pendingLoadHeistPath = string.IsNullOrWhiteSpace(sourcePath) ? _heistSessionStore.FilePath : sourcePath;
        IsLoadHeistConfirmationOpen = true;
    }

    [RelayCommand]
    private void CancelLoadHeist()
    {
        _pendingLoadHeistPath = null;
        IsLoadHeistConfirmationOpen = false;
    }

    [RelayCommand]
    private void ConfirmLoadHeist()
    {
        var sourcePath = _pendingLoadHeistPath;
        CancelLoadHeist();
        if (sourcePath is null) return;
        try
        {
            ApplyHeistSave(_heistSessionStore.Load(sourcePath));
            SaveHeistCore();
            SaveStatus = $"Loaded heist from {sourcePath}";
        }
        catch (Exception exception)
        {
            SaveStatus = $"Could not load heist: {exception.Message}";
        }
    }

    [RelayCommand]
    private void EndHeist() => IsEndHeistConfirmationOpen = true;

    [RelayCommand]
    private void CancelEndHeist() => IsEndHeistConfirmationOpen = false;

    [RelayCommand]
    private async Task ConfirmEndHeist()
    {
        IsEndHeistConfirmationOpen = false;
        if (MicrophoneStatus != MicrophoneStatus.Off)
            await StopVoiceRecognitionAsync();
        _heistSessionStore.DeleteCurrent();
        _lootRun.ResetLootState();
        PlayerCount = 1;
        CurrentStage = PlannerStage.Preparation;
        VaultCode = null;
        GuardsDown = 0;
        CamerasDown = 0;
        _disabledCameras.Clear();
        _voiceContext.Reset();
        _voiceUndo.Clear();
        FocusedMapId = null;
        SewerRouteInput = string.Empty;
        HighlightedSewerPathIds.Clear();
        IsSewerRouteComplete = false;
        foreach (var camera in SecurityCameras) camera.IsActive = true;
        foreach (var guard in SecurityGuards) guard.IsActive = true;
        foreach (var patrol in SecurityPatrols) patrol.IsActive = true;
        RefreshLootState();
        RefreshVoiceContextDiagnostics();
        _heistId = Guid.NewGuid();
        _heistCreatedAtUtc = DateTimeOffset.UtcNow;
        SaveStatus = "Current heist ended and all session progress was cleared.";
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
        RebuildVoiceCommandParser();
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

    partial void OnFocusedMapIdChanged(string? value) => RefreshVoiceContextDiagnostics();

    private void OnSecurityObjectChanged(object? sender, PropertyChangedEventArgs e)
    {
        SecurityRevision++;
        SecurityStatus = "Unsaved security changes.";
        if (sender is SecurityCameraViewModel && e.PropertyName == nameof(SecurityCameraViewModel.Name))
            RebuildVoiceCommandParser();
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

    private HeistSaveFile BuildHeistSave() => new()
    {
        HeistId = _heistId,
        CreatedAtUtc = _heistCreatedAtUtc,
        LastSavedAtUtc = DateTimeOffset.UtcNow,
        PlayerCount = PlayerCount,
        Stage = CurrentStage,
        VaultCode = VaultCode,
        GuardsDown = GuardsDown,
        CamerasDown = CamerasDown,
        FocusedMapId = FocusedMapId,
        LootStates = _lootRun.States.ToDictionary(state => state.SpawnId, state =>
            new HeistLootState(state.IsPresent, state.ScopedValue, state.IsBuyersRequest, state.IsLooted), StringComparer.Ordinal),
        SewerRuntimeState = new SewerRuntimeSaveState
        {
            RouteInput = SewerRouteInput,
            IsRouteComplete = IsSewerRouteComplete,
            HighlightedPathIds = HighlightedSewerPathIds.ToArray(),
        },
        SecurityRuntimeState = new SecurityRuntimeSaveState
        {
            ActiveCameraIds = SecurityCameras.Where(item => item.IsActive).Select(item => item.Id).ToArray(),
            ActiveGuardIds = SecurityGuards.Where(item => item.IsActive).Select(item => item.Id).ToArray(),
            ActivePatrolIds = SecurityPatrols.Where(item => item.IsActive).Select(item => item.Id).ToArray(),
            DisabledCameras = new Dictionary<string, CameraDisableMethod>(_disabledCameras, StringComparer.Ordinal),
            CountedCameraTakedowns = CamerasDown,
        },
    };

    private void SaveHeistCore() => _heistSessionStore.Save(BuildHeistSave());

    private void LoadCurrentHeistOnStartup()
    {
        if (!_heistSessionStore.Exists)
        {
            SaveStatus = "No current heist save found; started a fresh Preparation session.";
            return;
        }
        try
        {
            ApplyHeistSave(_heistSessionStore.Load());
            SaveStatus = $"Current heist restored from {_heistSessionStore.FilePath}";
        }
        catch (Exception exception)
        {
            SaveStatus = $"Current heist save could not be loaded: {exception.Message}. A fresh session was started; the bad file was preserved.";
        }
    }

    private void ApplyHeistSave(HeistSaveFile save)
    {
        var knownLootIds = _lootRun.States.Select(state => state.SpawnId).ToHashSet(StringComparer.Ordinal);
        var unknownLoot = save.LootStates.Keys.FirstOrDefault(id => !knownLootIds.Contains(id));
        if (unknownLoot is not null) throw new InvalidDataException($"Save references unknown loot ID '{unknownLoot}'.");
        var knownPaths = SewerPaths.Select(path => path.Id).ToHashSet(StringComparer.Ordinal);
        var unknownPath = save.SewerRuntimeState.HighlightedPathIds.FirstOrDefault(id => !knownPaths.Contains(id));
        if (unknownPath is not null) throw new InvalidDataException($"Save references unknown sewer path '{unknownPath}'.");
        ValidateRuntimeIds(save.SecurityRuntimeState.ActiveCameraIds, SecurityCameras.Select(item => item.Id), "camera");
        ValidateRuntimeIds(save.SecurityRuntimeState.DisabledCameras.Keys, SecurityCameras.Select(item => item.Id), "disabled camera");
        ValidateRuntimeIds(save.SecurityRuntimeState.ActiveGuardIds, SecurityGuards.Select(item => item.Id), "guard");
        ValidateRuntimeIds(save.SecurityRuntimeState.ActivePatrolIds, SecurityPatrols.Select(item => item.Id), "patrol");

        _lootRun.ResetLootState();
        foreach (var (id, saved) in save.LootStates)
        {
            var state = _lootRun.GetState(id);
            state.IsPresent = saved.IsPresent;
            state.ScopedValue = saved.ScopedValue;
            state.IsBuyersRequest = saved.IsBuyersRequest;
            state.IsLooted = saved.IsLooted;
        }
        _heistId = save.HeistId;
        _heistCreatedAtUtc = save.CreatedAtUtc;
        PlayerCount = save.PlayerCount;
        VaultCode = save.VaultCode;
        GuardsDown = save.GuardsDown;
        CamerasDown = save.SecurityRuntimeState.CountedCameraTakedowns ?? save.CamerasDown;
        CurrentStage = save.Stage;
        SewerRouteInput = save.SewerRuntimeState.RouteInput;
        IsSewerRouteComplete = save.SewerRuntimeState.IsRouteComplete;
        HighlightedSewerPathIds.Clear();
        foreach (var pathId in save.SewerRuntimeState.HighlightedPathIds) HighlightedSewerPathIds.Add(pathId);
        var activeCameras = save.SecurityRuntimeState.ActiveCameraIds.ToHashSet(StringComparer.Ordinal);
        var activeGuards = save.SecurityRuntimeState.ActiveGuardIds.ToHashSet(StringComparer.Ordinal);
        var activePatrols = save.SecurityRuntimeState.ActivePatrolIds.ToHashSet(StringComparer.Ordinal);
        foreach (var item in SecurityCameras) item.IsActive = activeCameras.Contains(item.Id);
        _disabledCameras.Clear();
        foreach (var (id, method) in save.SecurityRuntimeState.DisabledCameras)
        {
            _disabledCameras[id] = method;
            SecurityCameras.First(item => item.Id == id).IsActive = false;
        }
        foreach (var item in SecurityGuards) item.IsActive = activeGuards.Contains(item.Id);
        foreach (var item in SecurityPatrols) item.IsActive = activePatrols.Contains(item.Id);
        SewerRevision++;
        FocusedMapId = save.FocusedMapId is { } focus && CurrentPolicy.AllowsMap(focus) ? focus : null;
        if (FocusedMapId is { } selected) SelectedMap = KortzMapCatalog.GetById(selected);
        _voiceContext.Reset();
        _voiceUndo.Clear();
        RefreshLootState();
        RefreshVoiceContextDiagnostics();
        NotifyCameraRuntimeChanged();
    }

    private static void ValidateRuntimeIds(IEnumerable<string> savedIds, IEnumerable<string> authoredIds, string kind)
    {
        var known = authoredIds.ToHashSet(StringComparer.Ordinal);
        var unknown = savedIds.FirstOrDefault(id => !known.Contains(id));
        if (unknown is not null) throw new InvalidDataException($"Save references unknown {kind} ID '{unknown}'.");
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
        RebuildVoiceCommandParser();
        _scopeOutSession.SetListening(MicrophoneStatus != MicrophoneStatus.Off);
        OnPropertyChanged(nameof(ScopeOutState));
        LootRevision++;
        OnPropertyChanged(nameof(BuyersRequestStatus));
        OnPropertyChanged(nameof(ActivityLootSummary));
        RefreshPlanningSummary();
    }

    private void RebuildVoiceCommandParser() => _voiceCommandParser = new VoiceCommandParser(
        _lootRun.Definitions, KortzMapCatalog.Maps, SecurityCameras.Select(camera => camera.ToDomain()));

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
        OnPropertyChanged(nameof(PlanningAnalysis));
        OnPropertyChanged(nameof(PlanningValueRange));
        OnPropertyChanged(nameof(PlanningKnownValue));
        OnPropertyChanged(nameof(PlanningEstimatedRange));
        OnPropertyChanged(nameof(PlanningPotentialRange));
        OnPropertyChanged(nameof(RecommendedHaulRange));
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
        _voiceUiHeartbeatTimer.Stop();
        _voiceFeedbackTimer.Stop();
        _speechRecognitionService.SpeechRecognized -= OnSpeechRecognized;
        _speechRecognitionService.PartialTranscriptChanged -= OnPartialTranscriptChanged;
        _speechRecognitionService.RecognitionFailed -= OnRecognitionFailed;
        _audioCaptureService.FrameCaptured -= OnAudioFrameCaptured;
        _speechRecognitionService.Dispose();
        _audioCaptureService.Dispose();
    }
}
