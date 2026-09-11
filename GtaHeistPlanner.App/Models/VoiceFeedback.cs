namespace GtaHeistPlanner.App.Models;

public enum VoiceFeedbackKind { Success, NotRecognized, Rejected }

public sealed record VoiceFeedback(VoiceFeedbackKind Kind, string Message)
{
    public string Color => Kind switch
    {
        VoiceFeedbackKind.Success => "#63E67A",
        VoiceFeedbackKind.NotRecognized => "#F3C969",
        _ => "#FF7777",
    };
}
