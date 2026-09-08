using GtaHeistPlanner.App.ViewModels;
using GtaHeistPlanner.App.Services;
using Avalonia;
using GtaHeistPlanner.Core.Planning;
using GtaHeistPlanner.Voice;

namespace GtaHeistPlanner.Tests.Voice;

public sealed class MainViewModelVoiceWorkflowTests
{
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
        Assert.Equal(2, viewModel.CamerasDown);
        Assert.Contains("limit", viewModel.VoiceCommandError, StringComparison.OrdinalIgnoreCase);
        viewModel.ProcessRecognizedText("undo");
        Assert.Equal(1, viewModel.CamerasDown);

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
