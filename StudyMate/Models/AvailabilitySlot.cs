namespace StudyMate.Models
{
    /// <summary>
    /// One block of a student's weekly availability. Deliberately coarse — students
    /// reliably know they are "free Tuesday evenings", not that they are free 18:00-19:30.
    /// </summary>
    public class AvailabilitySlot
    {
        public int StudentId { get; set; }
        public Student Student { get; set; }

        public DayOfWeek Day { get; set; }

        public TimeBlock Block { get; set; }
    }
}
