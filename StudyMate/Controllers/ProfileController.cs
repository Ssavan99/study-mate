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
                CurrentStudent = student,
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
                model.CurrentStudent = await _db.Students.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.StudentId == studentId.Value);
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

        /// <summary>
        /// Adds a course the fixed list doesn't cover. There is no free API to check a
        /// course code against a real catalog, so this can only validate the shape
        /// (department letters + a number) and reuse an existing row when the normalized
        /// code already exists, rather than letting the course list fragment into
        /// near-duplicates ("CSCE310" vs "CSCE 310" vs "csce 310").
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCourse(string code, string title)
        {
            var studentId = User.GetStudentId();
            if (studentId == null) return Forbid();

            var normalizedCode = CourseCodeParser.Normalize(code);
            if (normalizedCode == null)
            {
                TempData["CourseError"] = "Enter a course as a department and number, like \"CSCE 310\".";
                return RedirectToAction(nameof(Edit));
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["CourseError"] = "Give the course a title.";
                return RedirectToAction(nameof(Edit));
            }

            var course = await _db.Courses.FirstOrDefaultAsync(c => c.Code == normalizedCode);
            if (course == null)
            {
                course = new Course
                {
                    Code = normalizedCode,
                    Title = title.Trim(),
                    Department = normalizedCode.Split(' ')[0]
                };
                _db.Courses.Add(course);
                await _db.SaveChangesAsync();
            }

            var alreadyEnrolled = await _db.Enrollments
                .AnyAsync(e => e.StudentId == studentId.Value && e.CourseId == course.CourseId);

            if (!alreadyEnrolled)
            {
                _db.Enrollments.Add(new Enrollment { StudentId = studentId.Value, CourseId = course.CourseId });
                await _db.SaveChangesAsync();
            }

            TempData["Saved"] = $"Added {course.Code}.";
            return RedirectToAction(nameof(Edit));
        }

        private const int MaxPhotoBytes = 1024 * 1024;

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadPhoto(IFormFile photo)
        {
            var studentId = User.GetStudentId();
            if (studentId == null) return Forbid();

            if (photo == null || photo.Length == 0)
            {
                TempData["PhotoError"] = "Choose an image first.";
                return RedirectToAction(nameof(Edit));
            }

            if (photo.Length > MaxPhotoBytes)
            {
                TempData["PhotoError"] = "That image is too large — the limit is 1MB.";
                return RedirectToAction(nameof(Edit));
            }

            await using var stream = photo.OpenReadStream();
            var bytes = new byte[photo.Length];
            var read = 0;
            while (read < bytes.Length)
            {
                var n = await stream.ReadAsync(bytes.AsMemory(read, bytes.Length - read));
                if (n == 0) break;
                read += n;
            }

            // The browser's declared content-type is client-supplied and not trustworthy;
            // sniff the real format from the file's own magic bytes instead.
            var contentType = SniffImageContentType(bytes);
            if (contentType == null)
            {
                TempData["PhotoError"] = "That doesn't look like a JPEG, PNG, or WebP image.";
                return RedirectToAction(nameof(Edit));
            }

            var student = await _db.Students.FirstOrDefaultAsync(s => s.StudentId == studentId.Value);
            if (student == null) return NotFound();

            student.PhotoData = bytes;
            student.PhotoContentType = contentType;
            await _db.SaveChangesAsync();

            TempData["Saved"] = "Profile photo updated.";
            return RedirectToAction(nameof(Edit));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemovePhoto()
        {
            var studentId = User.GetStudentId();
            if (studentId == null) return Forbid();

            var student = await _db.Students.FirstOrDefaultAsync(s => s.StudentId == studentId.Value);
            if (student == null) return NotFound();

            student.PhotoData = null;
            student.PhotoContentType = null;
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Edit));
        }

        /// <summary>
        /// Serves any student's uploaded photo — this has to be reachable for whoever the
        /// avatar belongs to, not just the signed-in viewer, since match cards render
        /// other students' photos. [Authorize] at the class level is the only gate: any
        /// signed-in student can view any other student's avatar, which is the same
        /// exposure every other page in the deck already has.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Photo(int id)
        {
            var student = await _db.Students
                .AsNoTracking()
                .Where(s => s.StudentId == id)
                .Select(s => new { s.PhotoData, s.PhotoContentType })
                .FirstOrDefaultAsync();

            if (student?.PhotoData == null)
            {
                return NotFound();
            }

            Response.Headers.CacheControl = "private, max-age=3600";
            return File(student.PhotoData, student.PhotoContentType);
        }

        private static string SniffImageContentType(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            {
                return "image/jpeg";
            }

            if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
                && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            {
                return "image/png";
            }

            if (bytes.Length >= 12
                && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F'
                && bytes[8] == 'W' && bytes[9] == 'E' && bytes[10] == 'B' && bytes[11] == 'P')
            {
                return "image/webp";
            }

            return null;
        }
    }
}
