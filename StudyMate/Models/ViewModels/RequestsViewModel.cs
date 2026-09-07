namespace StudyMate.Models.ViewModels
{
    public class RequestsViewModel
    {
        public int CurrentStudentId { get; set; }

        public IReadOnlyList<StudyRequest> Incoming { get; set; } = new List<StudyRequest>();

        public IReadOnlyList<StudyRequest> Outgoing { get; set; } = new List<StudyRequest>();

        public IReadOnlyList<StudyRequest> Connections { get; set; } = new List<StudyRequest>();

        /// <summary>
        /// Suggested study times for each accepted connection, keyed by
        /// <see cref="StudyRequest.StudyRequestId"/>. Populated by the controller from
        /// <see cref="Services.StudySessionSuggester"/> — the view only reads it.
        /// </summary>
        public IReadOnlyDictionary<int, IReadOnlyList<StudySuggestion>> StudySuggestions { get; set; } =
            new Dictionary<int, IReadOnlyList<StudySuggestion>>();

        /// <summary>The other party in an accepted connection, from the current student's point of view.</summary>
        public Student Partner(StudyRequest request) =>
            request.FromStudentId == CurrentStudentId ? request.ToStudent : request.FromStudent;

        /// <summary>Suggested times for one connection, or empty if none were found.</summary>
        public IReadOnlyList<StudySuggestion> SuggestionsFor(StudyRequest request) =>
            StudySuggestions.TryGetValue(request.StudyRequestId, out var suggestions)
                ? suggestions
                : Array.Empty<StudySuggestion>();
    }
}
