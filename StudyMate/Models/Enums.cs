namespace StudyMate.Models
{
    /// <summary>How much noise and conversation a student wants while studying.</summary>
    public enum NoiseLevel
    {
        Silent = 0,
        Quiet = 1,
        Discussion = 2
    }

    /// <summary>Whether a student works steadily or in bursts before deadlines.</summary>
    public enum StudyPace
    {
        Steady = 0,
        Mixed = 1,
        Crammer = 2
    }

    /// <summary>Preferred size of a study group.</summary>
    public enum GroupSize
    {
        OneOnOne = 0,
        SmallGroup = 1,
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
