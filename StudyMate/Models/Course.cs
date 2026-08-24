using System.ComponentModel.DataAnnotations;

namespace StudyMate.Models
{
    public class Course
    {
        public int CourseId { get; set; }

        /// <summary>
        /// Course code as students refer to it, e.g. "CSCE 310". Unique per university,
        /// not globally — different schools genuinely use different department
        /// abbreviations and numbering, so "CSCE 310" at one is not the same course as
        /// "CSCE 310" at another.
        /// </summary>
        [Required, StringLength(16)]
        public string Code { get; set; }

        [Required, StringLength(120)]
        public string Title { get; set; }

        /// <summary>The department abbreviation, derived from Code — e.g. "CSCE".</summary>
        [Required, StringLength(60)]
        public string Department { get; set; }

        /// <summary>
        /// Scopes the course to one school, so the picker only ever offers courses that
        /// exist where the student actually studies.
        /// </summary>
        [Required, StringLength(120)]
        public string University { get; set; }

        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }
}
