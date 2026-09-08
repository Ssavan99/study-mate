using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyMate.Data;
using StudyMate.Models;
using StudyMate.Services;

namespace StudyMate.Controllers;

[Authorize]
public class SafetyController(AppDbContext db) : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Block(int id)
    {
        var me = User.GetStudentId(); if (me == null || me == id) return Forbid();
        if (await db.Students.AnyAsync(s => s.StudentId == id) && !await db.Blocks.AnyAsync(b => b.BlockerStudentId == me && b.BlockedStudentId == id))
        {
            db.Blocks.Add(new Block { BlockerStudentId = me.Value, BlockedStudentId = id });
            db.StudyRequests.RemoveRange(db.StudyRequests.Where(r => (r.FromStudentId == me && r.ToStudentId == id) || (r.FromStudentId == id && r.ToStudentId == me)));
            await db.SaveChangesAsync();
        }
        return Redirect(Request.Headers.Referer.ToString());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Report(int id, ReportReason reason, string detail)
    {
        var me = User.GetStudentId(); if (me == null || me == id) return Forbid();
        if (!await db.Students.AnyAsync(s => s.StudentId == id)) return NotFound();
        db.Reports.Add(new Report { ReporterStudentId = me.Value, ReportedStudentId = id, Reason = reason, Detail = detail?.Trim() });
        if (!await db.Blocks.AnyAsync(b => b.BlockerStudentId == me && b.BlockedStudentId == id)) db.Blocks.Add(new Block { BlockerStudentId = me.Value, BlockedStudentId = id });
        db.StudyRequests.RemoveRange(db.StudyRequests.Where(r => (r.FromStudentId == me && r.ToStudentId == id) || (r.FromStudentId == id && r.ToStudentId == me)));
        await db.SaveChangesAsync();
        return Redirect(Request.Headers.Referer.ToString());
    }
}
