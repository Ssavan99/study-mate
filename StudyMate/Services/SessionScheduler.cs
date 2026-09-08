using StudyMate.Models;
namespace StudyMate.Services;
public static class SessionScheduler
{
 public static bool HasCapacity(int capacity, int participantCount) => participantCount < capacity;
 public static int DaysUntil(DayOfWeek day, DateTime today) => ((int)day - (int)today.DayOfWeek + 7) % 7;
 public static IReadOnlyList<StudySession> Rank(IEnumerable<StudySession> sessions, DateTime today) => sessions.OrderBy(s=>DaysUntil(s.Day,today)).ThenBy(s=>(int)s.Block).ToList();
 public static bool CanJoin(StudySession session, int studentId, IEnumerable<Block> blocks) => !session.IsCancelled && !session.Participants.Any(p=>p.StudentId==studentId) && HasCapacity(session.Capacity,session.Participants.Count) && !session.Participants.Any(p=>SafetyPolicy.IsBlockedPair(studentId,p.StudentId,blocks));
}
