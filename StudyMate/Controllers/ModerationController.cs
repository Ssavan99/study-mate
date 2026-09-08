using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyMate.Data;
using StudyMate.Models;

namespace StudyMate.Controllers;

[Authorize(Policy = "RequireAdmin")]
public class ModerationController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index() => View(await db.Reports.Where(r => r.Status == ReportStatus.Open).OrderByDescending(r => r.CreatedAt).ToListAsync());
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int id, bool suspend, string note)
    {
        var report = await db.Reports.FindAsync(id); if (report == null) return NotFound();
        report.Status = suspend ? ReportStatus.ActionTaken : ReportStatus.Dismissed; report.ReviewedAt = DateTime.UtcNow; report.ReviewerNote = note?.Trim();
        if (suspend) { var student = await db.Students.FindAsync(report.ReportedStudentId); if (student != null) student.IsSuspended = true; }
        await db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }
}
