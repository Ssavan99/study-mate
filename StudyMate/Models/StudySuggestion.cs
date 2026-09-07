namespace StudyMate.Models
{
    /// <summary>
    /// A concrete time two connected students could meet, derived from their shared
    /// free blocks (see <see cref="Services.StudySessionSuggester"/>). Courses are
    /// context for <see cref="Text"/>, not a requirement — a pair with overlapping
    /// availability but no shared seeking-partner course still gets a suggestion,
    /// just without a course mentioned.
    /// </summary>
    public class StudySuggestion
    {
        public DayOfWeek Day { get; init; }

        public TimeBlock Block { get; init; }

        public IReadOnlyList<Course> Courses { get; init; } = new List<Course>();

        /// <summary>Human-readable explanation, e.g. "You are both free Tuesday evening".</summary>
        public string Text { get; init; }
    }
}
