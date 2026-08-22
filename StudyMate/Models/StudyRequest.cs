namespace StudyMate.Models
{
    /// <summary>A request from one student to study with another, and its outcome.</summary>
    public class StudyRequest
    {
        public int StudyRequestId { get; set; }

        public int FromStudentId { get; set; }
        public Student FromStudent { get; set; }

        public int ToStudentId { get; set; }
        public Student ToStudent { get; set; }

        public RequestStatus Status { get; set; } = RequestStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? RespondedAt { get; set; }
    }
}
