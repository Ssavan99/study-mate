using System.ComponentModel.DataAnnotations;
namespace StudyMate.Models;
public enum ReportReason { Harassment, Spam, FakeProfile, Other }
public enum ReportStatus { Open, Reviewed, ActionTaken, Dismissed }
public class Report { public int Id { get; set; } public int ReporterStudentId { get; set; } public int ReportedStudentId { get; set; } public ReportReason Reason { get; set; } [StringLength(1000)] public string Detail { get; set; } public DateTime CreatedAt { get; set; } = DateTime.UtcNow; public ReportStatus Status { get; set; } = ReportStatus.Open; public DateTime? ReviewedAt { get; set; } [StringLength(1000)] public string ReviewerNote { get; set; } }
