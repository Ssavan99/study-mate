using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using Microsoft.EntityFrameworkCore;using StudyMate.Data;using StudyMate.Services;
namespace StudyMate.Controllers;
[Authorize(Policy="RequireAdmin")]
public class AdminController(AppDbContext db):Controller
{public async Task<IActionResult> Index(){var students=await db.Students.AsNoTracking().ToListAsync();var requests=await db.StudyRequests.AsNoTracking().ToListAsync();var sessions=await db.StudySessions.AsNoTracking().Include(s=>s.Participants).ToListAsync();var reports=await db.Reports.AsNoTracking().ToListAsync();var enrollments=await db.Enrollments.AsNoTracking().Include(e=>e.Course).ToListAsync();return View(DashboardMetrics.Calculate(students,requests,sessions,reports,enrollments));}}
