using System.ComponentModel.DataAnnotations;
namespace StudyMate.Models;
public class StudySession
{
 public int Id { get; set; } public int CourseId { get; set; } public Course Course { get; set; }
 public int HostStudentId { get; set; } public Student HostStudent { get; set; }
 public DayOfWeek Day { get; set; } public TimeBlock Block { get; set; }
 [Range(2,8)] public int Capacity { get; set; } = 4;
 [Required,StringLength(120)] public string Location { get; set; }
 [StringLength(500)] public string Note { get; set; } public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
 public bool IsCancelled { get; set; } public ICollection<SessionParticipant> Participants { get; set; } = new List<SessionParticipant>();
}
