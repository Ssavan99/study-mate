using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StudyMate.Controllers;
using StudyMate.Data;
using StudyMate.Models;
using Xunit;

namespace StudyMate.Tests
{
    /// <summary>
    /// Covers the two Phase D additions to the profile: adding a course by free text
    /// (format validation, dedup against an existing course) and uploading a profile
    /// photo (size cap, real content-sniffing rather than trusting the browser).
    /// </summary>
    public class ProfileCoursesAndPhotoTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public ProfileCoursesAndPhotoTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

            using var context = new AppDbContext(_options);
            context.Database.EnsureCreated();
            context.Students.Add(TestData.Student(1, "Test Student"));
            context.Courses.Add(TestData.Course(1, "CSCE 310"));
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

        // --- AddCourse --------------------------------------------------------

        [Fact]
        public async Task AddingANewCourseCode_CreatesTheCourseAndEnrolsTheStudent()
        {
            await using var context = new AppDbContext(_options);
            var controller = NewController(context);

            await controller.AddCourse("MATH 208", "Calculus III");

            var course = await context.Courses.SingleAsync(c => c.Code == "MATH 208");
            Assert.Equal("MATH", course.Department);
            Assert.True(await context.Enrollments.AnyAsync(e => e.StudentId == 1 && e.CourseId == course.CourseId));
        }

        [Fact]
        public async Task AddingACourseThatAlreadyExists_JoinsItRatherThanDuplicating()
        {
            await using var context = new AppDbContext(_options);
            var controller = NewController(context);

            // Differs only in casing and spacing from the seeded "CSCE 310".
            await controller.AddCourse("csce310", "Data Structures and Algorithms");

            var matching = await context.Courses.Where(c => c.Code == "CSCE 310").ToListAsync();
            Assert.Single(matching);
            Assert.True(await context.Enrollments.AnyAsync(e => e.StudentId == 1 && e.CourseId == matching[0].CourseId));
        }

        [Fact]
        public async Task AddingTheSameCourseTwice_DoesNotCreateADuplicateEnrolment()
        {
            await using var context = new AppDbContext(_options);
            var controller = NewController(context);

            await controller.AddCourse("MATH 208", "Calculus III");
            await controller.AddCourse("math 208", "Calculus III");

            var course = await context.Courses.SingleAsync(c => c.Code == "MATH 208");
            var count = await context.Enrollments.CountAsync(e => e.StudentId == 1 && e.CourseId == course.CourseId);
            Assert.Equal(1, count);
        }

        [Theory]
        [InlineData("not a code")]
        [InlineData("")]
        [InlineData("TOOLONGDEPARTMENT 310")]
        public async Task MalformedCourseCode_CreatesNothing(string code)
        {
            await using var context = new AppDbContext(_options);
            var controller = NewController(context);
            var before = await context.Courses.CountAsync();

            await controller.AddCourse(code, "Some Title");

            Assert.Equal(before, await context.Courses.CountAsync());
        }

        [Fact]
        public async Task BlankTitle_CreatesNothingEvenWithAValidCode()
        {
            await using var context = new AppDbContext(_options);
            var controller = NewController(context);

            await controller.AddCourse("MATH 208", "   ");

            Assert.False(await context.Courses.AnyAsync(c => c.Code == "MATH 208"));
        }

        // --- profile photo ------------------------------------------------------

        private static readonly byte[] PngHeader =
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00
        };

        private static IFormFile MakeFile(byte[] bytes, string name = "photo.png")
        {
            var stream = new MemoryStream(bytes);
            return new FormFile(stream, 0, bytes.Length, "photo", name);
        }

        [Fact]
        public async Task UploadingAValidImage_StoresItOnTheStudent()
        {
            await using var context = new AppDbContext(_options);
            var controller = NewController(context);

            await controller.UploadPhoto(MakeFile(PngHeader));

            var student = await context.Students.SingleAsync(s => s.StudentId == 1);
            Assert.NotNull(student.PhotoData);
            Assert.Equal("image/png", student.PhotoContentType);
        }

        [Fact]
        public async Task UploadingSomethingThatIsNotAnImage_IsRejected()
        {
            await using var context = new AppDbContext(_options);
            var controller = NewController(context);

            await controller.UploadPhoto(MakeFile(new byte[] { 0x00, 0x01, 0x02, 0x03 }));

            var student = await context.Students.SingleAsync(s => s.StudentId == 1);
            Assert.Null(student.PhotoData);
        }

        [Fact]
        public async Task UploadingAFileOverTheSizeCap_IsRejected()
        {
            await using var context = new AppDbContext(_options);
            var controller = NewController(context);

            var oversized = new byte[1024 * 1024 + 1];
            PngHeader.CopyTo(oversized, 0);

            await controller.UploadPhoto(MakeFile(oversized));

            var student = await context.Students.SingleAsync(s => s.StudentId == 1);
            Assert.Null(student.PhotoData);
        }

        [Fact]
        public async Task RemovingAPhoto_ClearsBothColumns()
        {
            await using var context = new AppDbContext(_options);
            var controller = NewController(context);
            await controller.UploadPhoto(MakeFile(PngHeader));

            await controller.RemovePhoto();

            var student = await context.Students.SingleAsync(s => s.StudentId == 1);
            Assert.Null(student.PhotoData);
            Assert.Null(student.PhotoContentType);
        }

        [Fact]
        public async Task PhotoAction_ServesTheStoredBytesWithTheSniffedContentType()
        {
            await using var context = new AppDbContext(_options);
            await NewController(context).UploadPhoto(MakeFile(PngHeader));

            await using var readContext = new AppDbContext(_options);
            var result = await NewController(readContext).Photo(1);

            var file = Assert.IsType<FileContentResult>(result);
            Assert.Equal("image/png", file.ContentType);
            Assert.Equal(PngHeader, file.FileContents);
        }

        [Fact]
        public async Task PhotoAction_ReturnsNotFoundWhenTheStudentHasNoPhoto()
        {
            await using var context = new AppDbContext(_options);
            var result = await NewController(context).Photo(1);

            Assert.IsType<NotFoundResult>(result);
        }

        public void Dispose() => _connection.Dispose();

        private sealed class NullTempDataProvider : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
            public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
        }
    }
}
