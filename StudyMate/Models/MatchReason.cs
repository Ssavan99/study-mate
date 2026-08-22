namespace StudyMate.Models
{
    public enum MatchReasonKind
    {
        SharedCourses,
        Availability,
        StudyStyle,
        Major
    }

    /// <summary>
    /// One contributing component of a match score. Every point a candidate earns is
    /// attributable to one of these, so the interface can always explain the number.
    /// </summary>
    public class MatchReason
    {
        public MatchReasonKind Kind { get; init; }

        /// <summary>Human-readable explanation, e.g. "2 shared courses: CSCE 310, MATH 208".</summary>
        public string Text { get; init; }

        public int Points { get; init; }
    }
}
