using GtaHeistPlanner.Core.Settings;

namespace GtaHeistPlanner.Voice;

public sealed class SpeechTranscriptionProviderRouter(Func<ApplicationSettings, ISpeechTranscriptionProvider> factory,
    VoiceDiagnosticTrace? diagnostics = null) : ISpeechRecognitionService
{
    private readonly VoiceDiagnosticTrace _diagnostics = diagnostics ?? new();
    private ISpeechTranscriptionProvider? _provider;
    private ApplicationSettings _settings = new();
    public event EventHandler<SpeechRecognizedEventArgs>? SpeechRecognized;
    public event EventHandler<PartialTranscriptEventArgs>? PartialTranscriptChanged;
    public event EventHandler<SpeechRecognitionFailedEventArgs>? RecognitionFailed;
    public string StateDescription => _provider?.StateDescription ?? "Stopped";
    public VoiceDiagnosticTrace Diagnostics => _diagnostics;
    public TranscriptionProviderKind SelectedKind => _settings.VoiceProvider;

    public async Task ConfigureAsync(ApplicationSettings settings, CancellationToken cancellationToken = default)
    {
        if (_provider is not null)
        {
            await _provider.StopAsync(cancellationToken).ConfigureAwait(false);
            Unsubscribe(_provider); _provider.Dispose(); _provider = null;
        }
        _settings = settings;
    }

    public async Task StartAsync(PcmAudioFormat format, IReadOnlyCollection<string> keywords, CancellationToken cancellationToken = default)
    {
        var provider = EnsureProvider();
        await provider.StartAsync(format, keywords, cancellationToken).ConfigureAwait(false);
    }

    public void PushAudio(ReadOnlyMemory<byte> pcmAudio, int activityLevel) => _provider?.PushAudio(pcmAudio, activityLevel);
    public Task StopAsync(CancellationToken cancellationToken = default) => _provider?.StopAsync(cancellationToken) ?? Task.CompletedTask;
    public Task<string> TestAsync(CancellationToken cancellationToken = default)
    {
        return EnsureProvider().TestAsync(cancellationToken);
    }
    private ISpeechTranscriptionProvider EnsureProvider()
    {
        if (_provider is not null) return _provider;
        _provider = factory(_settings); Subscribe(_provider); return _provider;
    }
    private void Subscribe(ISpeechTranscriptionProvider provider) { provider.SpeechRecognized += ForwardFinal; provider.PartialTranscriptChanged += ForwardPartial; provider.RecognitionFailed += ForwardFailure; }
    private void Unsubscribe(ISpeechTranscriptionProvider provider) { provider.SpeechRecognized -= ForwardFinal; provider.PartialTranscriptChanged -= ForwardPartial; provider.RecognitionFailed -= ForwardFailure; }
    private void ForwardFinal(object? sender, SpeechRecognizedEventArgs e) => SpeechRecognized?.Invoke(this, e);
    private void ForwardPartial(object? sender, PartialTranscriptEventArgs e) => PartialTranscriptChanged?.Invoke(this, e);
    private void ForwardFailure(object? sender, SpeechRecognitionFailedEventArgs e) => RecognitionFailed?.Invoke(this, e);
    public void Dispose() { if (_provider is not null) { Unsubscribe(_provider); _provider.Dispose(); } }
}
