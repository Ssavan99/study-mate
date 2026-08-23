namespace StudyMate.Services
{
    /// <summary>
    /// Tells the views which optional sign-in methods were configured at startup, so the
    /// GitHub button is only offered when the app can actually complete that flow.
    /// </summary>
    public record AuthenticationOptionsView(bool GitHubEnabled);
}
