using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyMate.Data;
using StudyMate.Models;

namespace StudyMate.Controllers;

[Authorize(Policy = "RequireAdmin")]
public class ModerationController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index() { ViewBag.CourseRequests = await db.CourseRequests.Where(r => r.Status == CourseRequestStatus.Pending).OrderByDescending(r => r.CreatedAt).ToListAsync(); return View(await db.Reports.Where(r => r.Status == ReportStatus.Open).OrderByDescending(r => r.CreatedAt).ToListAsync()); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int id, bool suspend, string note)
    {
        var report = await db.Reports.FindAsync(id); if (report == null) return NotFound();
        report.Status = suspend ? ReportStatus.ActionTaken : ReportStatus.Dismissed; report.ReviewedAt = DateTime.UtcNow; report.ReviewerNote = note?.Trim();
        if (suspend) { var student = await db.Students.FindAsync(report.ReportedStudentId); if (student != null) student.IsSuspended = true; }
        await db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewCourseRequest(int id, bool approve, string note)
    {
        var request = await db.CourseRequests.FindAsync(id); if (request == null) return NotFound();
        request.ReviewedAt = DateTime.UtcNow; request.ReviewerNote = note?.Trim(); request.Status = approve ? CourseRequestStatus.Approved : CourseRequestStatus.Rejected;
        if (approve && !await db.Courses.AnyAsync(c => c.UniversityId == request.UniversityId && c.Code == request.Code)) db.Courses.Add(new Course { UniversityId = request.UniversityId, Code = request.Code, Title = request.Title, Department = request.Code.Split(' ')[0] });
        await db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }
}
