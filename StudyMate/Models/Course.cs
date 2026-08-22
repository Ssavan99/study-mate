using System.ComponentModel.DataAnnotations;

namespace StudyMate.Models
{
    public class Course
    {
        public int CourseId { get; set; }

        /// <summary>Course code as students refer to it, e.g. "CSCE 310".</summary>
        [Required, StringLength(16)]
        public string Code { get; set; }

        [Required, StringLength(120)]
        public string Title { get; set; }

        [Required, StringLength(60)]
        public string Department { get; set; }

        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }
}
