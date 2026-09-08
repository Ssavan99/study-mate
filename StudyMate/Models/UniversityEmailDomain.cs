using System.ComponentModel.DataAnnotations;

namespace StudyMate.Models
{
    public class UniversityEmailDomain
    {
        public int Id { get; set; }
        public int UniversityId { get; set; }

        [Required, StringLength(253)]
        public string Domain { get; set; }

        public University University { get; set; }
    }
}
