namespace StudyMate.Services
{
    /// <summary>
    /// Tells the views which optional sign-in methods were configured at startup, so the
    /// provider button is only offered when the app can actually complete that flow.
    /// </summary>
    public record AuthenticationOptionsView(bool GitHubEnabled, bool MicrosoftEnabled);
}
