using System.ComponentModel.DataAnnotations;

namespace StudyMate.Models
{
    public class Student
    {
        public int StudentId { get; set; }

        [Required, StringLength(80)]
        public string Name { get; set; }

        [Required, EmailAddress, StringLength(160)]
        public string Email { get; set; }

        /// <summary>Hashed with ASP.NET Core's PasswordHasher. Never stores a plaintext password.</summary>
        [Required]
        public string PasswordHash { get; set; }

        [StringLength(80)]
        public string Major { get; set; }

        /// <summary>
        /// Hard-filters matching: only students who share a university ever appear in
        /// each other's decks. There is no catalog of real universities to validate
        /// against, so this is free text, same as Major.
        /// </summary>
        [Required, StringLength(120)]
        public string University { get; set; }

        /// <summary>Year of study, 1-5. 5 covers graduate students.</summary>
        [Range(1, 5)]
        public int Year { get; set; } = 1;

        [StringLength(280)]
        public string Bio { get; set; }

        public NoiseLevel PreferredNoise { get; set; } = NoiseLevel.Quiet;

        public StudyPace Pace { get; set; } = StudyPace.Mixed;

        public GroupSize PreferredGroupSize { get; set; } = GroupSize.Either;

        /// <summary>
        /// Marks one of the seeded personas offered for one-click sign-in on the landing page.
        /// Accounts created through registration are never demo accounts.
        /// </summary>
        public bool IsDemo { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

        public ICollection<AvailabilitySlot> Availability { get; set; } = new List<AvailabilitySlot>();
    }
}
