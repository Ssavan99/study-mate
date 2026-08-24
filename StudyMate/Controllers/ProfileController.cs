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
    public class ProfileController : Controller
    {
        private readonly AppDbContext _db;

        public ProfileController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var studentId = User.GetStudentId();
            if (studentId == null) return Forbid();

            var student = await _db.Students
                .Include(s => s.Enrollments)
                .Include(s => s.Availability)
                .FirstOrDefaultAsync(s => s.StudentId == studentId.Value);

            if (student == null) return NotFound();

            var model = new ProfileEditViewModel
            {
                Name = student.Name,
                Major = student.Major,
                University = student.University,
                Year = student.Year,
                Bio = student.Bio,
                PreferredNoise = student.PreferredNoise,
                Pace = student.Pace,
                PreferredGroupSize = student.PreferredGroupSize,
                SelectedCourseIds = student.Enrollments.Select(e => e.CourseId).ToList(),
                SelectedSlots = student.Availability
                    .Select(a => ProfileEditViewModel.SlotKey(a.Day, a.Block))
                    .ToList()
            };

            await PopulateCoursesAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProfileEditViewModel model)
        {
            var studentId = User.GetStudentId();
            if (studentId == null) return Forbid();

            if (!ModelState.IsValid)
            {
                await PopulateCoursesAsync(model);
                return View(model);
            }

            var student = await _db.Students
                .Include(s => s.Enrollments)
                .Include(s => s.Availability)
                .FirstOrDefaultAsync(s => s.StudentId == studentId.Value);

            if (student == null) return NotFound();

            student.Name = model.Name.Trim();
            student.Major = model.Major.Trim();
            student.University = model.University.Trim();
            student.Year = model.Year;
            student.Bio = model.Bio?.Trim();
            student.PreferredNoise = model.PreferredNoise;
            student.Pace = model.Pace;
            student.PreferredGroupSize = model.PreferredGroupSize;

            // Only course ids that actually exist are accepted, so a tampered form
            // cannot create enrolments against unknown courses.
            var desiredCourseIds = (await _db.Courses
                    .Where(c => model.SelectedCourseIds.Contains(c.CourseId))
                    .Select(c => c.CourseId)
                    .ToListAsync())
                .ToHashSet();

            // These collections are diffed rather than cleared and rebuilt. Both entities
            // have composite keys, so re-adding a row that is still in the change tracker
            // as Deleted collides on its key and SaveChanges throws. Diffing also avoids
            // rewriting every row on a save that changed nothing.
            foreach (var removed in student.Enrollments.Where(e => !desiredCourseIds.Contains(e.CourseId)).ToList())
            {
                student.Enrollments.Remove(removed);
            }

            var existingCourseIds = student.Enrollments.Select(e => e.CourseId).ToHashSet();
            foreach (var courseId in desiredCourseIds.Except(existingCourseIds))
            {
                student.Enrollments.Add(new Enrollment { StudentId = student.StudentId, CourseId = courseId });
            }

            var desiredSlots = new HashSet<(DayOfWeek Day, TimeBlock Block)>();
            foreach (var key in model.SelectedSlots)
            {
                if (ProfileEditViewModel.TryParseSlot(key, out var day, out var block))
                {
                    desiredSlots.Add((day, block));
                }
            }

            foreach (var removed in student.Availability.Where(a => !desiredSlots.Contains((a.Day, a.Block))).ToList())
            {
                student.Availability.Remove(removed);
            }

            var existingSlots = student.Availability.Select(a => (a.Day, a.Block)).ToHashSet();
            foreach (var (day, block) in desiredSlots.Except(existingSlots))
            {
                student.Availability.Add(new AvailabilitySlot
                {
                    StudentId = student.StudentId,
                    Day = day,
                    Block = block
                });
            }

            await _db.SaveChangesAsync();

            TempData["Saved"] = "Your profile has been updated.";
            return RedirectToAction(nameof(Edit));
        }

        private async Task PopulateCoursesAsync(ProfileEditViewModel model)
        {
            model.AllCourses = await _db.Courses
                .OrderBy(c => c.Department)
                .ThenBy(c => c.Code)
                .ToListAsync();
        }
    }
}
