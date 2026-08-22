namespace StudyMate.Models
{
    /// <summary>
    /// Records that a student chose "Not now" on a candidate, so the deck stops
    /// offering them. Distinct from a declined request: a pass is one-sided and
    /// the other student is never told.
    /// </summary>
    public class Pass
    {
        public int StudentId { get; set; }
        public Student Student { get; set; }

        public int PassedStudentId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
