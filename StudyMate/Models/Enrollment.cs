namespace StudyMate.Models
{
    /// <summary>Join entity: a student is taking a course this term.</summary>
    public class Enrollment
    {
        public int StudentId { get; set; }
        public Student Student { get; set; }

        public int CourseId { get; set; }
        public Course Course { get; set; }
    }
}
