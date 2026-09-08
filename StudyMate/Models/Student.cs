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

        /// <summary>Null until an institutional sign-in has verified the affiliation.</summary>
        public int? UniversityId { get; set; }

        public University University { get; set; }

        [EmailAddress, StringLength(160)]
        public string VerifiedEmail { get; set; }

        public DateTime? EmailVerifiedAt { get; set; }

        /// <summary>Year of study, 1-5. 5 covers graduate students.</summary>
        [Range(1, 5)]
        public int Year { get; set; } = 1;

        [StringLength(280)]
        public string Bio { get; set; }

        /// <summary>
        /// Optional uploaded profile photo, capped ~1MB and content-type sniffed at
        /// upload time. Null means "use the generated initials avatar instead."
        /// Stored in the database rather than a file host: consistent with everything
        /// else here resetting on restart, and needs no external service or signup.
        /// </summary>
        public byte[] PhotoData { get; set; }

        [StringLength(50)]
        public string PhotoContentType { get; set; }

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
