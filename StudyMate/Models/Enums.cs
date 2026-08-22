using System.ComponentModel.DataAnnotations;

namespace StudyMate.Models
{
    /// <summary>How much noise and conversation a student wants while studying.</summary>
    public enum NoiseLevel
    {
        [Display(Name = "Silent")]
        Silent = 0,

        [Display(Name = "Quiet")]
        Quiet = 1,

        [Display(Name = "Talking it through")]
        Discussion = 2
    }

    /// <summary>Whether a student works steadily or in bursts before deadlines.</summary>
    public enum StudyPace
    {
        [Display(Name = "A bit every week")]
        Steady = 0,

        [Display(Name = "A mix of both")]
        Mixed = 1,

        [Display(Name = "Bursts before deadlines")]
        Crammer = 2
    }

    /// <summary>Preferred size of a study group.</summary>
    public enum GroupSize
    {
        [Display(Name = "One-on-one")]
        OneOnOne = 0,

        [Display(Name = "Small group")]
        SmallGroup = 1,

        [Display(Name = "Either is fine")]
        Either = 2
    }

    /// <summary>Coarse blocks of the day, used for availability overlap.</summary>
    public enum TimeBlock
    {
        Morning = 0,
        Afternoon = 1,
        Evening = 2
    }

    public enum RequestStatus
    {
        Pending = 0,
        Accepted = 1,
        Declined = 2
    }
}
