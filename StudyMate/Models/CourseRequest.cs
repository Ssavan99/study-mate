using System.ComponentModel.DataAnnotations;
namespace StudyMate.Models;
public enum CourseRequestStatus { Pending, Approved, Rejected }
public class CourseRequest { public int Id { get; set; } public int StudentId { get; set; } public int UniversityId { get; set; } [Required,StringLength(16)] public string Code { get; set; } [Required,StringLength(120)] public string Title { get; set; } public DateTime CreatedAt { get; set; }=DateTime.UtcNow; public CourseRequestStatus Status { get; set; }=CourseRequestStatus.Pending; public DateTime? ReviewedAt { get; set; } [StringLength(1000)] public string ReviewerNote { get; set; } }
