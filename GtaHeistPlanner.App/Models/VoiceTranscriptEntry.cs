namespace GtaHeistPlanner.App.Models;

public sealed record VoiceTranscriptEntry(string Text, bool IsCommand)
{
    public string Color => IsCommand ? "#63E67A" : "#F2A65A";
}
