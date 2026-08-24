using System.ComponentModel.DataAnnotations;

namespace StudyMate.Models.ViewModels
{
    public class ProfileEditViewModel
    {
        [Required, StringLength(80, MinimumLength = 2)]
        [Display(Name = "Full name")]
        public string Name { get; set; }

        [Required, StringLength(80)]
        public string Major { get; set; }

        [Required, StringLength(120)]
        public string University { get; set; }

        [Range(1, 5)]
        public int Year { get; set; } = 1;

        [StringLength(280)]
        [Display(Name = "About you")]
        public string Bio { get; set; }

        [Display(Name = "Noise")]
        public NoiseLevel PreferredNoise { get; set; }

        [Display(Name = "Pace")]
        public StudyPace Pace { get; set; }

        [Display(Name = "Group size")]
        public GroupSize PreferredGroupSize { get; set; }

        /// <summary>Course ids the student is enrolled in.</summary>
        public List<int> SelectedCourseIds { get; set; } = new();

        /// <summary>Availability encoded as "day-block", e.g. "2-2" for Tuesday evening.</summary>
        public List<string> SelectedSlots { get; set; } = new();

        /// <summary>Populated by the controller for rendering; not posted back.</summary>
        public List<Course> AllCourses { get; set; } = new();

        /// <summary>Populated by the controller so the view can render the avatar; not posted back.</summary>
        public Student CurrentStudent { get; set; }

        public static string SlotKey(DayOfWeek day, TimeBlock block) => $"{(int)day}-{(int)block}";

        public static bool TryParseSlot(string key, out DayOfWeek day, out TimeBlock block)
        {
            day = default;
            block = default;

            var parts = key?.Split('-');
            if (parts is not { Length: 2 }) return false;
            if (!int.TryParse(parts[0], out var d) || d is < 0 or > 6) return false;
            if (!int.TryParse(parts[1], out var b) || !Enum.IsDefined(typeof(TimeBlock), b)) return false;

            day = (DayOfWeek)d;
            block = (TimeBlock)b;
            return true;
        }
    }
}
