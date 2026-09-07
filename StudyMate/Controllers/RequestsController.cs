using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyMate.Data;
using StudyMate.Models;
using StudyMate.Models.ViewModels;
using StudyMate.Services;

namespace StudyMate.Controllers
{
    [Authorize]
    public class RequestsController : Controller
    {
        private readonly AppDbContext _db;

        public RequestsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var studentId = User.GetStudentId();
            if (studentId == null) return Forbid();

            var incoming = await _db.StudyRequests
                .AsNoTracking()
                .Include(r => r.FromStudent)
                .Where(r => r.ToStudentId == studentId.Value && r.Status == RequestStatus.Pending)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var outgoing = await _db.StudyRequests
                .AsNoTracking()
                .Include(r => r.ToStudent)
                .Where(r => r.FromStudentId == studentId.Value)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // Two collection includes per side (Enrollments and Availability) multiply rows
            // together if run as one query, same reasoning as MatchService — split them.
            var connections = await _db.StudyRequests
                .AsNoTracking()
                .Include(r => r.FromStudent).ThenInclude(s => s.Enrollments).ThenInclude(e => e.Course)
                .Include(r => r.FromStudent).ThenInclude(s => s.Availability)
                .Include(r => r.ToStudent).ThenInclude(s => s.Enrollments).ThenInclude(e => e.Course)
                .Include(r => r.ToStudent).ThenInclude(s => s.Availability)
                .Where(r => r.Status == RequestStatus.Accepted &&
                            (r.FromStudentId == studentId.Value || r.ToStudentId == studentId.Value))
                .OrderByDescending(r => r.RespondedAt)
                .AsSplitQuery()
                .ToListAsync();

            var currentStudent = await _db.Students
                .AsNoTracking()
                .Include(s => s.Enrollments).ThenInclude(e => e.Course)
                .Include(s => s.Availability)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.StudentId == studentId.Value);

            // Suggested times are computed here, from tables already loaded above, rather
            // than in the view: the view should render, not call into services.
            var studySuggestions = new Dictionary<int, IReadOnlyList<StudySuggestion>>();
            if (currentStudent != null)
            {
                var today = DateTime.UtcNow.DayOfWeek;
                foreach (var connection in connections)
                {
                    var partner = connection.FromStudentId == studentId.Value
                        ? connection.ToStudent
                        : connection.FromStudent;

                    studySuggestions[connection.StudyRequestId] =
                        StudySessionSuggester.Suggest(currentStudent, partner, today);
                }
            }

            return View(new RequestsViewModel
            {
                CurrentStudentId = studentId.Value,
                Incoming = incoming,
                Outgoing = outgoing,
                Connections = connections,
                StudySuggestions = studySuggestions
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Accept(int id) => RespondAsync(id, RequestStatus.Accepted);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Decline(int id) => RespondAsync(id, RequestStatus.Declined);

        /// <summary>
        /// Only the recipient of a pending request may respond to it, so a guessed id
        /// cannot be used to answer somebody else's request.
        /// </summary>
        private async Task<IActionResult> RespondAsync(int requestId, RequestStatus status)
        {
            var studentId = User.GetStudentId();
            if (studentId == null) return Forbid();

            var request = await _db.StudyRequests.FirstOrDefaultAsync(r =>
                r.StudyRequestId == requestId &&
                r.ToStudentId == studentId.Value &&
                r.Status == RequestStatus.Pending);

            if (request == null)
            {
                return NotFound();
            }

            request.Status = status;
            request.RespondedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
