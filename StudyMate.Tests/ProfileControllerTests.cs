using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StudyMate.Controllers;
using StudyMate.Data;
using StudyMate.Models;
using StudyMate.Models.ViewModels;
using Xunit;

namespace StudyMate.Tests
{
    /// <summary>
    /// Saving the profile rewrites two collections whose entities have composite keys.
    /// Clearing and re-adding them collides in EF's change tracker, so these tests drive
    /// the real controller against a real database rather than asserting on a mock.
    /// </summary>
    public class ProfileControllerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public ProfileControllerTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

            using var context = new AppDbContext(_options);
            context.Database.EnsureCreated();

            context.Courses.AddRange(
                TestData.Course(1, "CSCE 310"),
                TestData.Course(2, "MATH 314"),
                TestData.Course(3, "PHYS 211"));

            context.Students.Add(TestData.Student(1, "Test Student"));
            context.SaveChanges();
        }

        private static ProfileController NewController(AppDbContext context)
        {
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "1") }, "Test");

            return new ProfileController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
                },
                TempData = new TempDataDictionary(new DefaultHttpContext(), new NullTempDataProvider())
            };
        }

        private static ProfileEditViewModel Model(IEnumerable<int> courseIds, IEnumerable<string> slots) => new()
        {
            Name = "Test Student",
            Major = "Computer Science",
            Year = 2,
            Bio = "Testing",
            PreferredNoise = NoiseLevel.Quiet,
            Pace = StudyPace.Steady,
            PreferredGroupSize = GroupSize.Either,
            SelectedCourseIds = courseIds.ToList(),
            SelectedSlots = slots.ToList()
        };

        private async Task SaveAsync(ProfileEditViewModel model)
        {
            await using var context = new AppDbContext(_options);
            var result = await NewController(context).Edit(model);
            Assert.IsType<RedirectToActionResult>(result);
        }

        private async Task<(List<int> Courses, List<string> Slots)> LoadAsync()
        {
            await using var context = new AppDbContext(_options);
            var student = await context.Students
                .Include(s => s.Enrollments)
                .Include(s => s.Availability)
                .SingleAsync(s => s.StudentId == 1);

            return (student.Enrollments.Select(e => e.CourseId).OrderBy(i => i).ToList(),
                    student.Availability.Select(a => ProfileEditViewModel.SlotKey(a.Day, a.Block)).OrderBy(k => k).ToList());
        }

        [Fact]
        public async Task SavingTheSameProfileTwice_DoesNotThrow()
        {
            // The original implementation cleared both collections and re-added identical
            // rows, which threw on the second save because the deleted entries still held
            // their keys in the change tracker.
            var model = Model(new[] { 1, 2 }, new[] { "1-2", "3-2" });

            await SaveAsync(model);
            await SaveAsync(Model(new[] { 1, 2 }, new[] { "1-2", "3-2" }));

            var (courses, slots) = await LoadAsync();
            Assert.Equal(new[] { 1, 2 }, courses);
            Assert.Equal(new[] { "1-2", "3-2" }, slots);
        }

        [Fact]
        public async Task AddingAndRemovingCourses_IsPersistedExactly()
        {
            await SaveAsync(Model(new[] { 1, 2 }, Array.Empty<string>()));
            await SaveAsync(Model(new[] { 2, 3 }, Array.Empty<string>()));

            var (courses, _) = await LoadAsync();
            Assert.Equal(new[] { 2, 3 }, courses);
        }

        [Fact]
        public async Task AddingAndRemovingAvailability_IsPersistedExactly()
        {
            await SaveAsync(Model(Array.Empty<int>(), new[] { "1-0", "2-1" }));
            await SaveAsync(Model(Array.Empty<int>(), new[] { "2-1", "5-2" }));

            var (_, slots) = await LoadAsync();
            Assert.Equal(new[] { "2-1", "5-2" }, slots);
        }

        [Fact]
        public async Task ClearingEverything_LeavesNoRowsBehind()
        {
            await SaveAsync(Model(new[] { 1, 2, 3 }, new[] { "1-0", "2-1", "3-2" }));
            await SaveAsync(Model(Array.Empty<int>(), Array.Empty<string>()));

            var (courses, slots) = await LoadAsync();
            Assert.Empty(courses);
            Assert.Empty(slots);
        }

        [Fact]
        public async Task CourseIdsThatDoNotExist_AreIgnored()
        {
            // A tampered form must not create enrolments against unknown courses.
            await SaveAsync(Model(new[] { 1, 999 }, Array.Empty<string>()));

            var (courses, _) = await LoadAsync();
            Assert.Equal(new[] { 1 }, courses);
        }

        [Fact]
        public async Task MalformedAvailabilityKeys_AreIgnored()
        {
            await SaveAsync(Model(Array.Empty<int>(), new[] { "1-2", "not-a-slot", "9-9", "", "3" }));

            var (_, slots) = await LoadAsync();
            Assert.Equal(new[] { "1-2" }, slots);
        }

        [Fact]
        public async Task DuplicateSelections_DoNotCreateDuplicateRows()
        {
            await SaveAsync(Model(new[] { 1, 1, 2 }, new[] { "1-2", "1-2" }));

            var (courses, slots) = await LoadAsync();
            Assert.Equal(new[] { 1, 2 }, courses);
            Assert.Equal(new[] { "1-2" }, slots);
        }

        [Fact]
        public async Task ScalarFieldsAreSaved()
        {
            var model = Model(Array.Empty<int>(), Array.Empty<string>());
            model.Name = "Renamed Student";
            model.Major = "Physics";
            model.Year = 4;
            model.PreferredNoise = NoiseLevel.Discussion;

            await SaveAsync(model);

            await using var context = new AppDbContext(_options);
            var student = await context.Students.SingleAsync(s => s.StudentId == 1);

            Assert.Equal("Renamed Student", student.Name);
            Assert.Equal("Physics", student.Major);
            Assert.Equal(4, student.Year);
            Assert.Equal(NoiseLevel.Discussion, student.PreferredNoise);
        }

        public void Dispose() => _connection.Dispose();

        private sealed class NullTempDataProvider : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
        }
    }
}
