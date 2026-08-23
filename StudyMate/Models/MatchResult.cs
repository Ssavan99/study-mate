namespace StudyMate.Models
{
    /// <summary>A scored candidate, with the breakdown that produced the score.</summary>
    public class MatchResult
    {
        public Student Candidate { get; init; }

        /// <summary>Total score, 0-100.</summary>
        public int Score { get; init; }

        public IReadOnlyList<MatchReason> Reasons { get; init; } = new List<MatchReason>();

        public IReadOnlyList<Course> SharedCourses { get; init; } = new List<Course>();

        public IReadOnlyList<AvailabilitySlot> SharedSlots { get; init; } = new List<AvailabilitySlot>();
    }
}
