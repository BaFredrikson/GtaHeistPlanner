using GtaHeistPlanner.App.ViewModels;
using GtaHeistPlanner.App.Services;
using GtaHeistPlanner.App.Models;
using Avalonia;
using GtaHeistPlanner.Core.Planning;
using GtaHeistPlanner.Voice;

namespace GtaHeistPlanner.Tests.Voice;

public sealed class MainViewModelVoiceWorkflowTests
{
    [Theory]
    [InlineData("[BLANK AUDIO]")]
    [InlineData(" [blank audio] ")]
    [InlineData("   ")]
    public void NonSpeechTranscriptNeverReachesNormalCommandPipeline(string transcript)
    {
        using var viewModel = CreateListeningViewModel();
        var feedback = viewModel.CurrentVoiceFeedback;

        viewModel.ProcessRecognizedText(transcript);

        Assert.Null(viewModel.LastRecognizedText);
        Assert.Null(viewModel.LastParsedVoiceCommand);
        Assert.Same(feedback, viewModel.CurrentVoiceFeedback);
        Assert.Empty(viewModel.VoiceTranscript);
        Assert.Contains("ignored", viewModel.VoiceStartupDiagnostics, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecentVoiceHistoryRetainsOnlyNewestSevenMeaningfulEntries()
    {
        using var viewModel = CreateListeningViewModel();
        for (var index = 1; index <= 8; index++)
            viewModel.ProcessRecognizedText($"meaningful unknown phrase {index}");

        Assert.Equal(7, viewModel.VoiceTranscript.Count);
        Assert.DoesNotContain(viewModel.VoiceTranscript, entry => entry.Text.EndsWith("1", StringComparison.Ordinal));
        Assert.Equal("meaningful unknown phrase 8", viewModel.VoiceTranscript[^1].Text);

        viewModel.ProcessRecognizedText("[BLANK AUDIO]");
        Assert.Equal(7, viewModel.VoiceTranscript.Count);
        Assert.Equal("meaningful unknown phrase 8", viewModel.VoiceTranscript[^1].Text);
    }

    [Theory]
    [InlineData("118 thousand", "500", 118500)]
    [InlineData("34 thousand", "five hundred", 34500)]
    [InlineData("105 thousand", "seven hundred fifty", 105750)]
    public void SplitNumericValueContinuesPreviousLootAssignment(string thousands, string remainder, int expected)
    {
        using var viewModel = CreateListeningViewModel();
        var loot = viewModel.LootMarkers.Single(marker => marker.Id == "basement-loot-02");
        viewModel.ProcessRecognizedText("scope out");
        viewModel.ProcessRecognizedText("vault painting right right");
        viewModel.ProcessRecognizedText(thousands);
        viewModel.ProcessRecognizedText(remainder);

        Assert.Equal(expected, loot.ScopedValue);
        Assert.Contains("Numeric continuation", viewModel.VoiceStartupDiagnostics ?? string.Empty);
        viewModel.ProcessRecognizedText("undo");
        Assert.Null(loot.ScopedValue);
        Assert.True(loot.IsPresent);
    }

    [Fact]
    public void MapCommandInterruptsNumericContinuation()
    {
        using var viewModel = CreateListeningViewModel();
        var loot = viewModel.LootMarkers.Single(marker => marker.Id == "basement-loot-02");
        viewModel.ProcessRecognizedText("scope out");
        viewModel.ProcessRecognizedText("vault painting right right");
        viewModel.ProcessRecognizedText("118 thousand");
        viewModel.ProcessRecognizedText("pull up main floor");
        viewModel.ProcessRecognizedText("500");
        Assert.Equal(118000, loot.ScopedValue);
    }

    [Fact]
    public void MentioningDifferentLootMovesPendingTargetWithoutChangingPreviousValue()
    {
        using var viewModel = CreateListeningViewModel();
        var west = viewModel.LootMarkers.Single(marker => marker.Id == "basement-loot-02");
        var east = viewModel.LootMarkers.Single(marker => marker.Id == "basement-loot-03");
        viewModel.ProcessRecognizedText("scope out");
        viewModel.ProcessRecognizedText("vault painting right right");
        viewModel.ProcessRecognizedText("118 thousand");
        viewModel.ProcessRecognizedText("vault painting left left");
        viewModel.ProcessRecognizedText("500");
        Assert.Equal(118000, west.ScopedValue);
        Assert.Equal(500, east.ScopedValue);
    }

    [Fact]
    public void StageLabelGuideAndFeedbackReflectFieldUseOutcomes()
    {
        using var viewModel = CreateListeningViewModel();
        Assert.Equal("Heist", viewModel.StageOptions.Single(option => option.Stage == PlannerStage.HeistActivity).Label);

        viewModel.ProcessRecognizedText("start infiltration");
        Assert.Equal(VoiceFeedbackKind.Success, viewModel.CurrentVoiceFeedback?.Kind);
        Assert.Contains("Guard down", viewModel.CurrentVoiceGuideGroups.Single(group => group.Name == "Guards").Phrases, StringComparer.OrdinalIgnoreCase);

        viewModel.ProcessRecognizedText("nonsense field phrase");
        Assert.Equal(VoiceFeedbackKind.NotRecognized, viewModel.CurrentVoiceFeedback?.Kind);

        viewModel.ProcessRecognizedText("camera down");
        viewModel.ProcessRecognizedText("camera disabled");
        viewModel.ProcessRecognizedText("disabled camera");
        Assert.Equal(VoiceFeedbackKind.Success, viewModel.CurrentVoiceFeedback?.Kind);
        Assert.Contains("stealth broken", viewModel.CurrentVoiceFeedback!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidAndInvalidPendingSewerRoutesPreserveExpectedState()
    {
        using var viewModel = CreateListeningViewModel();
        viewModel.ProcessRecognizedText("sewer grate reached");
        Assert.Equal("sewer", viewModel.FocusedMapId);
        Assert.Contains("SewerRoute", viewModel.VoiceContextSummary);

        viewModel.ProcessRecognizedText("two see five bee");
        Assert.Contains("SewerRoute", viewModel.VoiceContextSummary);
        Assert.Contains("Parsed: 2C 5B", viewModel.VoiceCommandError);

        viewModel.ProcessRecognizedText("two see one bee");
        Assert.DoesNotContain("SewerRoute", viewModel.VoiceContextSummary);
        Assert.True(viewModel.IsSewerRouteComplete);
        Assert.Equal("sewer", viewModel.FocusedMapId);
    }

    [Fact]
    public void SewerRouteBuildsIncrementallyAndSupportsCorrectionCommands()
    {
        using var viewModel = CreateListeningViewModel();
        viewModel.ProcessRecognizedText("sewer grate reached");
        viewModel.ProcessRecognizedText("two Charlie");
        Assert.Equal("2C", viewModel.SewerRouteInput);
        Assert.Contains("next chamber 1", viewModel.SewerDiagnostic, StringComparison.OrdinalIgnoreCase);

        viewModel.ProcessRecognizedText("back");
        Assert.Equal(string.Empty, viewModel.SewerRouteInput);
        viewModel.ProcessRecognizedText("two Charlie");
        viewModel.ProcessRecognizedText("one Bravo");
        Assert.Equal("2C 1B", viewModel.SewerRouteInput);
        Assert.True(viewModel.IsSewerRouteComplete);

        viewModel.ProcessRecognizedText("sewer route");
        viewModel.ProcessRecognizedText("clear route");
        Assert.Equal(string.Empty, viewModel.SewerRouteInput);
    }

    [Theory]
    [InlineData("so we're")]
    [InlineData("so we're uh")]
    [InlineData("so we're up")]
    [InlineData("route")]
    public void AmbiguousActivationWorksOnlyWhenSewerMapIsFocused(string phrase)
    {
        using var viewModel = CreateListeningViewModel();
        viewModel.CurrentStage = PlannerStage.HeistInfiltration;
        viewModel.FocusMapCommand.Execute("sewer");
        viewModel.ProcessRecognizedText(phrase);
        Assert.Contains("SewerRoute", viewModel.VoiceContextSummary);
    }

    [Fact]
    public void DirectSewerRouteFocusesSewerAndAppliesRoute()
    {
        using var viewModel = CreateListeningViewModel();

        viewModel.ProcessRecognizedText("sewer route two see one bee");

        Assert.Equal(PlannerStage.HeistInfiltration, viewModel.CurrentStage);
        Assert.Equal("sewer", viewModel.FocusedMapId);
        Assert.Equal("2C 1B", viewModel.SewerRouteInput);
        Assert.True(viewModel.IsSewerRouteComplete);
    }

    [Fact]
    public void LootContinuationSpecialLootActivityAndUndoUseOneDispatcher()
    {
        using var viewModel = CreateListeningViewModel();
        var loot = viewModel.LootMarkers.Single(marker => marker.Id == "basement-loot-01");

        viewModel.ProcessRecognizedText("scope out");
        viewModel.ProcessRecognizedText("truck cargo");
        viewModel.ProcessRecognizedText("118 thousand");
        viewModel.ProcessRecognizedText("special loot");
        Assert.True(loot.IsPresent);
        Assert.Equal(118000, loot.ScopedValue);
        Assert.True(loot.IsBuyersRequest);

        viewModel.ProcessRecognizedText("special loot");
        Assert.False(loot.IsBuyersRequest);
        viewModel.ProcessRecognizedText("undo");
        Assert.True(loot.IsBuyersRequest);

        viewModel.ProcessRecognizedText("heist start");
        viewModel.ProcessRecognizedText("going down skylight");
        viewModel.ProcessRecognizedText("truck cargo");
        Assert.True(loot.IsLooted);
        viewModel.ProcessRecognizedText("undo");
        Assert.False(loot.IsLooted);
    }

    [Fact]
    public void MapVaultCountersAndNewHeistManageTransientState()
    {
        using var viewModel = CreateListeningViewModel();
        viewModel.ProcessRecognizedText("pull up main floor");
        Assert.Equal("main-floor", viewModel.FocusedMapId);
        viewModel.ProcessRecognizedText("back up");
        Assert.Null(viewModel.FocusedMapId);

        viewModel.ProcessRecognizedText("vault code");
        viewModel.ProcessRecognizedText("forty six eighteen seventy three");
        Assert.Equal("46-18-73", viewModel.VaultCode);

        viewModel.ProcessRecognizedText("start infiltration");
        viewModel.ProcessRecognizedText("dropped a guard");
        viewModel.ProcessRecognizedText("charlie down");
        viewModel.ProcessRecognizedText("camera down");
        viewModel.ProcessRecognizedText("camera down");
        Assert.Equal(1, viewModel.GuardsDown);
        Assert.Equal(3, viewModel.CamerasDown);
        Assert.True(viewModel.IsCameraStealthCompromised);
        viewModel.ProcessRecognizedText("undo");
        Assert.Equal(2, viewModel.CamerasDown);

        viewModel.PlayerCount = 3;
        viewModel.NewHeistCommand.Execute(null);
        Assert.True(viewModel.IsNewHeistConfirmationOpen);
        viewModel.ConfirmNewHeistCommand.Execute(null);
        Assert.Equal(3, viewModel.PlayerCount);
        Assert.Equal(PlannerStage.HeistInfiltration, viewModel.CurrentStage);
        Assert.Equal("46-18-73", viewModel.VaultCode);
        Assert.Equal(0, viewModel.GuardsDown);
        Assert.Equal(0, viewModel.CamerasDown);
    }

    [Theory]
    [InlineData("camera down")]
    [InlineData("charlie down")]
    [InlineData("camera disabled")]
    [InlineData("disabled camera")]
    public void GenericCameraAliasesWorkDuringInfiltration(string phrase)
    {
        using var viewModel = CreateListeningViewModel();
        viewModel.ProcessRecognizedText("start infiltration");

        viewModel.ProcessRecognizedText(phrase);

        Assert.Equal(1, viewModel.CamerasDown);
        Assert.DoesNotContain("available during", viewModel.VoiceCommandError ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        viewModel.ProcessRecognizedText("undo");
        Assert.Equal(0, viewModel.CamerasDown);
    }

    [Fact]
    public void CameraTakedownAndGuideRemainAvailableDuringHeistActivity()
    {
        using var viewModel = CreateListeningViewModel();
        viewModel.ProcessRecognizedText("start infiltration");
        viewModel.ProcessRecognizedText("using access codes");

        viewModel.ProcessRecognizedText("camera down");

        Assert.Equal(PlannerStage.HeistActivity, viewModel.CurrentStage);
        Assert.Equal(1, viewModel.CamerasDown);
        Assert.Contains(viewModel.CurrentVoiceGuideGroups.SelectMany(group => group.Phrases),
            phrase => string.Equals(phrase, "camera down", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NamedAndShowroomCameraDisablesAreIdempotentAndCountCorrectly()
    {
        using var viewModel = CreateListeningViewModel();
        Assert.Equal("#63E67A", viewModel.CameraStatusColor);
        viewModel.ProcessRecognizedText("start infiltration");
        viewModel.ProcessRecognizedText("shot the button");
        Assert.False(viewModel.SecurityCameras.Single(camera => camera.Id == "main-floor-camera-03").IsActive);
        Assert.Equal(1, viewModel.TotalDisabledCameras);
        Assert.Equal(0, viewModel.CountedCameraTakedowns);
        viewModel.ProcessRecognizedText("using access codes");

        viewModel.ProcessRecognizedText("backstage camera down");
        viewModel.ProcessRecognizedText("backstage camera down");
        Assert.Equal(1, viewModel.CountedCameraTakedowns);
        Assert.Equal(2, viewModel.TotalDisabledCameras);
        Assert.Equal("#E8A65A", viewModel.CameraStatusColor);

        viewModel.ProcessRecognizedText("security office camera disabled");
        Assert.Equal(2, viewModel.CountedCameraTakedowns);
        Assert.False(viewModel.IsCameraStealthCompromised);
        viewModel.ProcessRecognizedText("downstairs camera down");
        Assert.Equal(3, viewModel.CountedCameraTakedowns);
        Assert.True(viewModel.IsCameraStealthCompromised);
        Assert.Equal("#FF7777", viewModel.CameraStatusColor);
    }

    [Fact]
    public void SaveLoadPreservesCameraDisableMethodAndCount()
    {
        var path = TempSavePath();
        using (var source = CreateListeningViewModel(path))
        {
            source.ProcessRecognizedText("start infiltration");
            source.ProcessRecognizedText("shot the button");
            source.ProcessRecognizedText("using access codes");
            source.ProcessRecognizedText("backstage camera down");
            source.SaveHeistCommand.Execute(null);
        }

        using var restored = CreateListeningViewModel(path);
        Assert.False(restored.SecurityCameras.Single(camera => camera.Id == "main-floor-camera-03").IsActive);
        Assert.False(restored.SecurityCameras.Single(camera => camera.Id == "main-floor-camera-01").IsActive);
        Assert.Equal(2, restored.TotalDisabledCameras);
        Assert.Equal(1, restored.CountedCameraTakedowns);
        Assert.False(restored.IsCameraStealthCompromised);
    }

    [Fact]
    public void SkylightAndButtonDisablesUndoWithoutChangingCount()
    {
        using var viewModel = CreateListeningViewModel();
        var showroom = viewModel.SecurityCameras.Single(camera => camera.Id == "main-floor-camera-03");
        viewModel.ProcessRecognizedText("start infiltration");
        viewModel.ProcessRecognizedText("going down skylight");
        Assert.False(showroom.IsActive);
        Assert.Equal(0, viewModel.CountedCameraTakedowns);
        viewModel.ProcessRecognizedText("undo");
        Assert.True(showroom.IsActive);
        Assert.Equal(PlannerStage.HeistInfiltration, viewModel.CurrentStage);

        viewModel.ProcessRecognizedText("shot the button");
        Assert.False(showroom.IsActive);
        viewModel.ProcessRecognizedText("undo");
        Assert.True(showroom.IsActive);
        Assert.Equal(0, viewModel.CountedCameraTakedowns);
    }

    [Fact]
    public void NamedGuardIsIdempotentCountedAndUndoable()
    {
        using var viewModel = CreateListeningViewModel();
        var guard = viewModel.SecurityGuards.Single(item => item.Id == "lower-floor-guard-01");
        viewModel.ProcessRecognizedText("start infiltration");
        viewModel.ProcessRecognizedText("using access codes");

        viewModel.ProcessRecognizedText("I got Front Desk");
        Assert.False(guard.IsActive);
        Assert.Equal(1, viewModel.GuardsDown);
        Assert.Contains("Front desk guard", viewModel.CurrentVoiceFeedback!.Message, StringComparison.OrdinalIgnoreCase);

        viewModel.ProcessRecognizedText("Front Desk is down");
        Assert.False(guard.IsActive);
        Assert.Equal(1, viewModel.GuardsDown);
        Assert.Contains("already down", viewModel.CurrentVoiceFeedback!.Message, StringComparison.OrdinalIgnoreCase);

        viewModel.ProcessRecognizedText("undo");
        Assert.True(guard.IsActive);
        Assert.Equal(0, viewModel.GuardsDown);
    }

    [Fact]
    public void NewAttemptPreservesPreparationAndClearsRunProgress()
    {
        using var viewModel = CreateListeningViewModel();
        viewModel.PlayerCount = 4;
        viewModel.VaultCode = "46-18-73";
        viewModel.ProcessRecognizedText("scope out");
        viewModel.ProcessRecognizedText("truck cargo 118 thousand");
        viewModel.ProcessRecognizedText("special loot");
        viewModel.ProcessRecognizedText("start infiltration");
        viewModel.ProcessRecognizedText("tango down");
        viewModel.ProcessRecognizedText("camera down");
        viewModel.ProcessRecognizedText("going down skylight");
        viewModel.ProcessRecognizedText("truck cargo");
        viewModel.SewerRouteInput = "2C";
        viewModel.HighlightedSewerPathIds.Add("temporary-runtime-path");
        var loot = viewModel.LootMarkers.Single(marker => marker.Id == "basement-loot-01");

        viewModel.NewHeistCommand.Execute(null);
        viewModel.ConfirmNewHeistCommand.Execute(null);

        Assert.Equal(4, viewModel.PlayerCount);
        Assert.Equal("46-18-73", viewModel.VaultCode);
        Assert.True(loot.IsPresent);
        Assert.Equal(118000, loot.ScopedValue);
        Assert.True(loot.IsBuyersRequest);
        Assert.False(loot.IsLooted);
        Assert.Equal(0, viewModel.GuardsDown);
        Assert.Equal(0, viewModel.CamerasDown);
        Assert.Empty(viewModel.HighlightedSewerPathIds);
        Assert.Equal(string.Empty, viewModel.SewerRouteInput);
        Assert.Contains("pending: None", viewModel.VoiceContextSummary);
        Assert.Contains("undo: 0", viewModel.VoiceContextSummary);
    }

    [Fact]
    public async Task EndHeistDeletesCurrentSaveAndResetsEverything()
    {
        var path = TempSavePath();
        using var viewModel = CreateListeningViewModel(path);
        var authoredCount = viewModel.LootMarkers.Count;
        viewModel.PlayerCount = 3;
        viewModel.ProcessRecognizedText("scope out");
        viewModel.ProcessRecognizedText("truck cargo 118 thousand");
        viewModel.SaveHeistCommand.Execute(null);
        Assert.True(File.Exists(path));

        viewModel.EndHeistCommand.Execute(null);
        Assert.True(viewModel.IsEndHeistConfirmationOpen);
        await viewModel.ConfirmEndHeistCommand.ExecuteAsync(null);

        Assert.False(File.Exists(path));
        Assert.Equal(1, viewModel.PlayerCount);
        Assert.Equal(PlannerStage.Preparation, viewModel.CurrentStage);
        Assert.All(viewModel.LootMarkers, loot => Assert.False(loot.IsPresent || loot.IsLooted || loot.IsBuyersRequest || loot.ScopedValue is not null));
        Assert.Equal(authoredCount, viewModel.LootMarkers.Count);
    }

    [Fact]
    public void StartupRestoresCurrentSaveAndCorruptSaveFailsSafely()
    {
        var path = TempSavePath();
        using (var source = CreateListeningViewModel(path))
        {
            source.PlayerCount = 2;
            source.ProcessRecognizedText("scope out");
            source.ProcessRecognizedText("truck cargo 118 thousand");
            source.SaveHeistCommand.Execute(null);
        }
        using (var restored = CreateListeningViewModel(path))
        {
            Assert.Equal(2, restored.PlayerCount);
            Assert.Equal(118000, restored.LootMarkers.Single(marker => marker.Id == "basement-loot-01").ScopedValue);
        }

        File.WriteAllText(path, "{broken");
        using var safe = CreateListeningViewModel(path);
        Assert.Equal(1, safe.PlayerCount);
        Assert.Contains("could not be loaded", safe.SaveStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("{broken", File.ReadAllText(path));
    }

    private static MainViewModel CreateListeningViewModel(string? path = null)
    {
        if (Application.Current is null)
            AppBuilder.Configure<Application>().UsePlatformDetect().SetupWithoutStarting();
        path ??= TempSavePath();
        var viewModel = new MainViewModel(new FakeSpeech(), new FakeCapture(), new HeistSessionStore(path));
        viewModel.MicrophoneStatus = MicrophoneStatus.Listening;
        return viewModel;
    }

    private static string TempSavePath() => Path.Combine(Path.GetTempPath(), $"gta-heist-vm-{Guid.NewGuid():N}", "current-heist.json");

    private sealed class FakeSpeech : ISpeechRecognitionService
    {
        public event EventHandler<SpeechRecognizedEventArgs>? SpeechRecognized { add { } remove { } }
        public event EventHandler<PartialTranscriptEventArgs>? PartialTranscriptChanged { add { } remove { } }
        public event EventHandler<SpeechRecognitionFailedEventArgs>? RecognitionFailed { add { } remove { } }
        public string StateDescription => "Test";
        public VoiceDiagnosticTrace Diagnostics { get; } = new();
        public Task StartAsync(PcmAudioFormat format, IReadOnlyCollection<string> keywords, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void PushAudio(ReadOnlyMemory<byte> pcmAudio, int activityLevel) { }
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
    }

    private sealed class FakeCapture : IAudioCaptureService
    {
        public event EventHandler<PcmAudioFrameEventArgs>? FrameCaptured { add { } remove { } }
        public Stream PcmStream => Stream.Null;
        public PcmAudioFormat Format { get; } = new(48_000, 16, 1);
        public string? ActiveDeviceId => "test";
        public VoiceDiagnosticTrace Diagnostics { get; } = new();
        public void Start(string endpointId) { }
        public void Stop() { }
        public void Dispose() { }
    }
}
