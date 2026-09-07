using System.ComponentModel.DataAnnotations;

namespace StudyMate.Models
{
    public class University
    {
        public int UniversityId { get; set; }

        [Required, StringLength(120)]
        public string Name { get; set; }

        [Required, StringLength(140)]
        public string Slug { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<UniversityEmailDomain> EmailDomains { get; set; } = new List<UniversityEmailDomain>();
        public ICollection<Student> Students { get; set; } = new List<Student>();
        public ICollection<Course> Courses { get; set; } = new List<Course>();
    }
}
