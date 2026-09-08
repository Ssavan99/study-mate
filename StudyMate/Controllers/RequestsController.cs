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
            var blockedIds = SafetyPolicy.ExcludedStudentIds(studentId.Value,
                await _db.Blocks.AsNoTracking().ToListAsync());

            var incoming = await _db.StudyRequests
                .AsNoTracking()
                .Include(r => r.FromStudent)
                .Where(r => r.ToStudentId == studentId.Value && r.Status == RequestStatus.Pending &&
                            !blockedIds.Contains(r.FromStudentId) && !r.FromStudent.IsSuspended)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var outgoing = await _db.StudyRequests
                .AsNoTracking()
                .Include(r => r.ToStudent)
                .Where(r => r.FromStudentId == studentId.Value && !blockedIds.Contains(r.ToStudentId) && !r.ToStudent.IsSuspended)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var connections = await _db.StudyRequests
                .AsNoTracking()
                .Include(r => r.FromStudent)
                .Include(r => r.ToStudent)
                .Where(r => r.Status == RequestStatus.Accepted &&
                            (r.FromStudentId == studentId.Value || r.ToStudentId == studentId.Value))
                .Where(r => !blockedIds.Contains(r.FromStudentId == studentId.Value ? r.ToStudentId : r.FromStudentId) &&
                            !r.FromStudent.IsSuspended && !r.ToStudent.IsSuspended)
                .OrderByDescending(r => r.RespondedAt)
                .ToListAsync();

            return View(new RequestsViewModel
            {
                CurrentStudentId = studentId.Value,
                Incoming = incoming,
                Outgoing = outgoing,
                Connections = connections
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
