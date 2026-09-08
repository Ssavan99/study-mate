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

        /// <summary>
        /// One more card for the client-side stack. `exclude` only needs to list the
        /// StudentIds already rendered on the client — anyone already decided (Pass row,
        /// StudyRequest row) is filtered out by <see cref="IMatchService.GetMatchesAsync"/>
        /// already, so this never needs to know about them.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Card([FromQuery] int[] exclude)
        {
            var studentId = User.GetStudentId();
            if (studentId == null) return Forbid();

            var results = await _matches.GetMatchesAsync(studentId.Value);

            var excludeSet = exclude == null || exclude.Length == 0
                ? null
                : new HashSet<int>(exclude);

            var next = excludeSet == null
                ? results.FirstOrDefault()
                : results.FirstOrDefault(m => !excludeSet.Contains(m.Candidate.StudentId));

            if (next == null)
            {
                return NoContent();
            }

            return PartialView("_MatchCard", next);
        }

        /// <summary>True when the request came from the deck's own background fetch rather
        /// than a real (or no-JS) form submission — the two cases answer differently.</summary>
        private bool IsAjax() => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

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

            if (!await _db.Students.AnyAsync(s => s.StudentId == id && !s.IsSuspended) ||
                SafetyPolicy.IsBlockedPair(studentId.Value, id, await _db.Blocks.AsNoTracking().ToListAsync()))
            {
                return NotFound();
            }

            // A request may already exist in the other direction — they asked first. Saying
            // nothing would look like the button did not work, so point at the inbox where
            // the request is actually waiting.
            var incoming = await _db.StudyRequests.AnyAsync(r =>
                r.FromStudentId == id && r.ToStudentId == studentId.Value);

            if (incoming)
            {
                var alreadyWaitingMessage = "They already asked you — their request is waiting in Requests.";

                if (IsAjax())
                {
                    var remainingOnIncoming = (await _matches.GetMatchesAsync(studentId.Value)).Count;
                    return Json(new { ok = true, message = alreadyWaitingMessage, remaining = remainingOnIncoming });
                }

                TempData["Sent"] = alreadyWaitingMessage;
                return RedirectToAction(nameof(Index));
            }

            var alreadySent = await _db.StudyRequests.AnyAsync(r =>
                r.FromStudentId == studentId.Value && r.ToStudentId == id);

            string sentMessage = null;
            if (!alreadySent)
            {
                _db.StudyRequests.Add(new StudyRequest
                {
                    FromStudentId = studentId.Value,
                    ToStudentId = id
                });
                await _db.SaveChangesAsync();
                sentMessage = "Study request sent.";
            }

            if (IsAjax())
            {
                var remaining = (await _matches.GetMatchesAsync(studentId.Value)).Count;
                return Json(new { ok = true, message = sentMessage, remaining });
            }

            if (sentMessage != null)
            {
                TempData["Sent"] = sentMessage;
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

            if (IsAjax())
            {
                var remaining = (await _matches.GetMatchesAsync(studentId.Value)).Count;
                return Json(new { ok = true, message = (string)null, remaining });
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
