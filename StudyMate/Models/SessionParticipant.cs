namespace StudyMate.Models;
public class SessionParticipant { public int SessionId { get; set; } public StudySession Session { get; set; } public int StudentId { get; set; } public Student Student { get; set; } public DateTime JoinedAt { get; set; } = DateTime.UtcNow; }
