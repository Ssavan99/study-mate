namespace StudyMate.Models
{
    /// <summary>
    /// Join entity: a student is taking this course. The full set of enrolments is the
    /// student's semester schedule; only those flagged <see cref="SeekingPartner"/>
    /// take part in matching.
    /// </summary>
    public class Enrollment
    {
        public int StudentId { get; set; }
        public Student Student { get; set; }

        public int CourseId { get; set; }
        public Course Course { get; set; }

        /// <summary>
        /// Whether the student wants a study partner for this specific course. Being
        /// enrolled in a course is not the same as wanting company in it, and a course
        /// only counts toward a match when both students have opted in.
        /// </summary>
        public bool SeekingPartner { get; set; } = true;
    }
}
