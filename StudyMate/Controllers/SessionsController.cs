using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyMate.Data;
using StudyMate.Models;
using StudyMate.Services;
namespace StudyMate.Controllers;
[Authorize]
public class SessionsController(AppDbContext db) : Controller
{
 public async Task<IActionResult> Index() { var me=User.GetStudentId(); if(me==null)return Forbid(); var student=await db.Students.FindAsync(me); if(student?.UniversityId==null)return View(Array.Empty<StudySession>()); var courses=await db.Enrollments.Where(e=>e.StudentId==me).Select(e=>e.CourseId).ToListAsync(); var sessions=await db.StudySessions.Include(s=>s.Course).Include(s=>s.Participants).Where(s=>!s.IsCancelled && courses.Contains(s.CourseId) && s.Course.UniversityId==student.UniversityId).ToListAsync(); return View(SessionScheduler.Rank(sessions,DateTime.Today)); }
 public async Task<IActionResult> Detail(int id) { var session=await db.StudySessions.Include(s=>s.Course).Include(s=>s.Participants).ThenInclude(p=>p.Student).FirstOrDefaultAsync(s=>s.Id==id); return session==null?NotFound():View(session); }
    /// <summary>
    /// The form for hosting a session. Only courses already on the student's own
    /// schedule are offered — a session for a course you are not taking would never
    /// surface to anyone, since browsing is filtered by enrolment.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var me = User.GetStudentId();
        if (me == null) return Forbid();
        await PopulateCourseChoices(me.Value);
        return View(new StudySession { Capacity = 4 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StudySession session)
    {
        var me = User.GetStudentId();
        if (me == null) return Forbid();

        // Only ever host for a course you are enrolled in. Enforced server-side: the
        // dropdown restricts the choice, but the post can be forged.
        var enrolled = await db.Enrollments
            .AnyAsync(e => e.StudentId == me.Value && e.CourseId == session.CourseId);
        if (!enrolled)
            ModelState.AddModelError(nameof(session.CourseId), "Pick a course from your own schedule.");

        if (session.Capacity < 2 || session.Capacity > 8)
            ModelState.AddModelError(nameof(session.Capacity), "A session holds between 2 and 8 people.");

        if (string.IsNullOrWhiteSpace(session.Location))
            ModelState.AddModelError(nameof(session.Location), "Say where you plan to meet.");

        if (!ModelState.IsValid)
        {
            await PopulateCourseChoices(me.Value);
            return View(session);
        }

        session.HostStudentId = me.Value;
        session.CreatedAt = DateTime.UtcNow;
        session.IsCancelled = false;
        db.StudySessions.Add(session);
        await db.SaveChangesAsync();

        // The host counts as a participant, so capacity and the participant list mean
        // the same thing for them as for everyone who joins later.
        db.SessionParticipants.Add(new SessionParticipant { SessionId = session.Id, StudentId = me.Value });
        await db.SaveChangesAsync();

        return RedirectToAction(nameof(Detail), new { id = session.Id });
    }

    private async Task PopulateCourseChoices(int studentId)
    {
        ViewBag.Courses = await db.Enrollments
            .Where(e => e.StudentId == studentId)
            .Include(e => e.Course)
            .Select(e => e.Course)
            .OrderBy(c => c.Code)
            .ToListAsync();
    }

 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Join(int id) { var me=User.GetStudentId(); if(me==null)return Forbid(); var session=await db.StudySessions.Include(s=>s.Participants).ThenInclude(p=>p.Student).FirstOrDefaultAsync(s=>s.Id==id); if(session==null)return NotFound(); var blocks=await db.Blocks.ToListAsync(); if(SessionScheduler.CanJoin(session,me.Value,blocks)){db.SessionParticipants.Add(new SessionParticipant{SessionId=id,StudentId=me.Value});await db.SaveChangesAsync();} return RedirectToAction(nameof(Detail),new{id}); }
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Leave(int id) { var me=User.GetStudentId(); if(me==null)return Forbid(); var p=await db.SessionParticipants.FindAsync(id,me.Value); if(p==null)return NotFound(); var session=await db.StudySessions.FindAsync(id); if(session?.HostStudentId==me)session.IsCancelled=true; else db.SessionParticipants.Remove(p); await db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }
}
