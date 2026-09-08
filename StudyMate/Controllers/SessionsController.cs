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
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Join(int id) { var me=User.GetStudentId(); if(me==null)return Forbid(); var session=await db.StudySessions.Include(s=>s.Participants).ThenInclude(p=>p.Student).FirstOrDefaultAsync(s=>s.Id==id); if(session==null)return NotFound(); var blocks=await db.Blocks.ToListAsync(); if(SessionScheduler.CanJoin(session,me.Value,blocks)){db.SessionParticipants.Add(new SessionParticipant{SessionId=id,StudentId=me.Value});await db.SaveChangesAsync();} return RedirectToAction(nameof(Detail),new{id}); }
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Leave(int id) { var me=User.GetStudentId(); if(me==null)return Forbid(); var p=await db.SessionParticipants.FindAsync(id,me.Value); if(p==null)return NotFound(); var session=await db.StudySessions.FindAsync(id); if(session?.HostStudentId==me)session.IsCancelled=true; else db.SessionParticipants.Remove(p); await db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }
}
