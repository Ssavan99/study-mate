namespace StudyMate.Models.ViewModels
{
    /// <summary>One row in the student's own schedule on the profile page.</summary>
    public class EnrolledCourseView
    {
        public int CourseId { get; set; }
        public string Code { get; set; }
        public string Title { get; set; }
        public string Department { get; set; }
        public bool SeekingPartner { get; set; }
    }
}
