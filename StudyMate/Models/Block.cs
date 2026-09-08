namespace StudyMate.Models;
public class Block { public int Id { get; set; } public int BlockerStudentId { get; set; } public int BlockedStudentId { get; set; } public DateTime CreatedAt { get; set; } = DateTime.UtcNow; }
