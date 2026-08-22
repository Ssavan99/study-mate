using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StudyMate.Data;
using StudyMate.Models;
using StudyMate.Services;
using Xunit;

namespace StudyMate.Tests
{
    /// <summary>
    /// Exercises the exclusion rules and ordering against a real SQLite database held in
    /// memory, so the EF query translation is tested rather than mocked away.
    /// </summary>
    public class MatchServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public MatchServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = new AppDbContext(_options);
            context.Database.EnsureCreated();
            Seed(context);
        }

        private static void Seed(AppDbContext context)
        {
            var algorithms = TestData.Course(1, "CSCE 310");
            var linearAlgebra = TestData.Course(2, "MATH 314");
            context.Courses.AddRange(algorithms, linearAlgebra);

            // 1 = the viewer. 2 shares both courses, 3 shares one, 4 shares none.
            var viewer = TestData.Student(1, "Viewer").WithCourses(algorithms, linearAlgebra);
            var strong = TestData.Student(2, "Strong Match").WithCourses(algorithms, linearAlgebra);
            var partial = TestData.Student(3, "Partial Match").WithCourses(algorithms);
            var weak = TestData.Student(4, "Weak Match");

            context.Students.AddRange(viewer, strong, partial, weak);
            context.SaveChanges();
        }

        private MatchService NewService(AppDbContext context) => new(context);

        [Fact]
        public async Task RanksStrongerMatchesFirst()
        {
            using var context = new AppDbContext(_options);

            var matches = await NewService(context).GetMatchesAsync(1);

            Assert.Equal(new[] { "Strong Match", "Partial Match", "Weak Match" },
                matches.Select(m => m.Candidate.Name).ToArray());
        }

        [Fact]
        public async Task NeverReturnsTheViewerThemselves()
        {
            using var context = new AppDbContext(_options);

            var matches = await NewService(context).GetMatchesAsync(1);

            Assert.DoesNotContain(matches, m => m.Candidate.StudentId == 1);
        }

        [Fact]
        public async Task ExcludesStudentsTheViewerHasAlreadySentARequestTo()
        {
            using (var arrange = new AppDbContext(_options))
            {
                arrange.StudyRequests.Add(new StudyRequest { FromStudentId = 1, ToStudentId = 2 });
                await arrange.SaveChangesAsync();
            }

            using var context = new AppDbContext(_options);
            var matches = await NewService(context).GetMatchesAsync(1);

            Assert.DoesNotContain(matches, m => m.Candidate.StudentId == 2);
        }

        [Fact]
        public async Task ExcludesStudentsWhoHaveSentTheViewerARequest()
        {
            using (var arrange = new AppDbContext(_options))
            {
                arrange.StudyRequests.Add(new StudyRequest { FromStudentId = 3, ToStudentId = 1 });
                await arrange.SaveChangesAsync();
            }

            using var context = new AppDbContext(_options);
            var matches = await NewService(context).GetMatchesAsync(1);

            Assert.DoesNotContain(matches, m => m.Candidate.StudentId == 3);
        }

        [Fact]
        public async Task ExcludesStudentsTheViewerHasPassedOn()
        {
            using (var arrange = new AppDbContext(_options))
            {
                arrange.Passes.Add(new Pass { StudentId = 1, PassedStudentId = 2 });
                await arrange.SaveChangesAsync();
            }

            using var context = new AppDbContext(_options);
            var matches = await NewService(context).GetMatchesAsync(1);

            Assert.DoesNotContain(matches, m => m.Candidate.StudentId == 2);
        }

        [Fact]
        public async Task APassByAnotherStudentDoesNotHideThemFromTheViewer()
        {
            using (var arrange = new AppDbContext(_options))
            {
                // Student 2 passed on the viewer; that is one-sided and must not
                // remove student 2 from the viewer's own deck.
                arrange.Passes.Add(new Pass { StudentId = 2, PassedStudentId = 1 });
                await arrange.SaveChangesAsync();
            }

            using var context = new AppDbContext(_options);
            var matches = await NewService(context).GetMatchesAsync(1);

            Assert.Contains(matches, m => m.Candidate.StudentId == 2);
        }

        [Fact]
        public async Task RespectsTheTakeLimit()
        {
            using var context = new AppDbContext(_options);

            var matches = await NewService(context).GetMatchesAsync(1, take: 2);

            Assert.Equal(2, matches.Count);
        }

        [Fact]
        public async Task ReturnsEmptyForAStudentThatDoesNotExist()
        {
            using var context = new AppDbContext(_options);

            var matches = await NewService(context).GetMatchesAsync(999);

            Assert.Empty(matches);
        }

        [Fact]
        public async Task LoadsSharedCoursesSoTheReasonsCanNameThem()
        {
            using var context = new AppDbContext(_options);

            var top = (await NewService(context).GetMatchesAsync(1)).First();

            Assert.Equal(2, top.SharedCourses.Count);
            Assert.Contains(top.Reasons, r => r.Kind == MatchReasonKind.SharedCourses && r.Text.Contains("CSCE 310"));
        }

        public void Dispose() => _connection.Dispose();
    }
}
