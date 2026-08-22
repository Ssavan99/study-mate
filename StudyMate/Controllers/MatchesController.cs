using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyMate.Data;
using StudyMate.Models;
using StudyMate.Services;

namespace StudyMate.Controllers
{
    [Authorize]
    public class MatchesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IMatchService _matches;

        public MatchesController(AppDbContext db, IMatchService matches)
        {
            _db = db;
            _matches = matches;
        }

        /// <summary>The review deck: candidates one at a time, best first.</summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var studentId = User.GetStudentId();
            if (studentId == null) return Forbid();

            var results = await _matches.GetMatchesAsync(studentId.Value);
            return View(results);
        }

        /// <summary>The same ranking as the deck, shown in full so the ordering is visible.</summary>
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var studentId = User.GetStudentId();
            if (studentId == null) return Forbid();

            var results = await _matches.GetMatchesAsync(studentId.Value);
            return View(results);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Connect(int id)
        {
            var studentId = User.GetStudentId();
            if (studentId == null) return Forbid();

            if (id == studentId.Value)
            {
                return BadRequest();
            }

            if (!await _db.Students.AnyAsync(s => s.StudentId == id))
            {
                return NotFound();
            }

            var alreadyExists = await _db.StudyRequests.AnyAsync(r =>
                (r.FromStudentId == studentId.Value && r.ToStudentId == id) ||
                (r.FromStudentId == id && r.ToStudentId == studentId.Value));

            if (!alreadyExists)
            {
                _db.StudyRequests.Add(new StudyRequest
                {
                    FromStudentId = studentId.Value,
                    ToStudentId = id
                });
                await _db.SaveChangesAsync();
                TempData["Sent"] = "Study request sent.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pass(int id)
        {
            var studentId = User.GetStudentId();
            if (studentId == null) return Forbid();

            if (id == studentId.Value)
            {
                return BadRequest();
            }

            var alreadyPassed = await _db.Passes
                .AnyAsync(p => p.StudentId == studentId.Value && p.PassedStudentId == id);

            if (!alreadyPassed && await _db.Students.AnyAsync(s => s.StudentId == id))
            {
                _db.Passes.Add(new Pass { StudentId = studentId.Value, PassedStudentId = id });
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
