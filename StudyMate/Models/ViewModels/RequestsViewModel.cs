namespace StudyMate.Models.ViewModels
{
    public class RequestsViewModel
    {
        public int CurrentStudentId { get; set; }

        public IReadOnlyList<StudyRequest> Incoming { get; set; } = new List<StudyRequest>();

        public IReadOnlyList<StudyRequest> Outgoing { get; set; } = new List<StudyRequest>();

        public IReadOnlyList<StudyRequest> Connections { get; set; } = new List<StudyRequest>();

        /// <summary>The other party in an accepted connection, from the current student's point of view.</summary>
        public Student Partner(StudyRequest request) =>
            request.FromStudentId == CurrentStudentId ? request.ToStudent : request.FromStudent;
    }
}
