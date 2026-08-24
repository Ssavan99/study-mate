using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StudyMate.Data;
using StudyMate.Models;
using StudyMate.Services;
using Xunit;

namespace StudyMate.Tests
{
    /// <summary>
    /// A course only counts toward a match when BOTH students have switched it on.
    /// Sitting in the same lecture as someone who isn't looking for company is not a
    /// match signal, and these tests pin that in the scorer and end to end.
    /// </summary>
    public class SeekingPartnerMatchingTests : IDisposable
    {
        private static readonly Course Algorithms = TestData.Course(1, "CSCE 310");
        private static readonly Course LinearAlgebra = TestData.Course(2, "MATH 314");

        [Fact]
        public void ACourseBothStudentsSwitchedOn_Counts()
        {
            var viewer = TestData.Student(1).WithCourses(Algorithms);
            var candidate = TestData.Student(2).WithCourses(Algorithms);

            Assert.Single(MatchScorer.SharedCourseIds(viewer, candidate));
        }

        [Fact]
        public void ACourseTheCandidateSwitchedOff_DoesNotCount()
        {
            var viewer = TestData.Student(1).WithCourses(Algorithms);
            var candidate = TestData.Student(2).WithCoursesNotSeeking(Algorithms);

            Assert.Empty(MatchScorer.SharedCourseIds(viewer, candidate));
        }

        [Fact]
        public void ACourseTheViewerSwitchedOff_DoesNotCount()
        {
            var viewer = TestData.Student(1).WithCoursesNotSeeking(Algorithms);
            var candidate = TestData.Student(2).WithCourses(Algorithms);

            Assert.Empty(MatchScorer.SharedCourseIds(viewer, candidate));
        }

        [Fact]
        public void OnlyTheOverlapThatBothSwitchedOn_Counts()
        {
            var viewer = TestData.Student(1).WithCourses(Algorithms).WithCoursesNotSeeking(LinearAlgebra);
            var candidate = TestData.Student(2).WithCourses(Algorithms, LinearAlgebra);

            var shared = MatchScorer.SharedCourseIds(viewer, candidate);

            Assert.Single(shared);
            Assert.Contains(Algorithms.CourseId, shared);
        }

        [Fact]
        public void SwitchingACourseOff_LowersTheScore()
        {
            var viewer = TestData.Student(1).WithCourses(Algorithms, LinearAlgebra);
            var seeking = TestData.Student(2).WithCourses(Algorithms, LinearAlgebra);
            var notSeeking = TestData.Student(3)
                .WithCourses(Algorithms)
                .WithCoursesNotSeeking(LinearAlgebra);

            Assert.True(MatchScorer.Score(viewer, seeking).Score > MatchScorer.Score(viewer, notSeeking).Score);
        }

        [Fact]
        public void AReasonNeverNamesACourseTheOtherStudentSwitchedOff()
        {
            var viewer = TestData.Student(1).WithCourses(Algorithms, LinearAlgebra);
            var candidate = TestData.Student(2)
                .WithCourses(Algorithms)
                .WithCoursesNotSeeking(LinearAlgebra);

            var result = MatchScorer.Score(viewer, candidate);
            var courseReason = result.Reasons.SingleOrDefault(r => r.Kind == MatchReasonKind.SharedCourses);

            Assert.NotNull(courseReason);
            Assert.Contains("CSCE 310", courseReason.Text);
            Assert.DoesNotContain("MATH 314", courseReason.Text);
        }

        // --- end to end through MatchService ---------------------------------

        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public SeekingPartnerMatchingTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

            using var context = new AppDbContext(_options);
            context.Database.EnsureCreated();
            context.Courses.Add(TestData.Course(1, "CSCE 310"));
            context.Students.AddRange(
                TestData.Student(1, "Viewer"),
                TestData.Student(2, "Also Looking"),
                TestData.Student(3, "Not Looking"));
            context.SaveChanges();

            context.Enrollments.AddRange(
                new Enrollment { StudentId = 1, CourseId = 1, SeekingPartner = true },
                new Enrollment { StudentId = 2, CourseId = 1, SeekingPartner = true },
                new Enrollment { StudentId = 3, CourseId = 1, SeekingPartner = false });
            context.SaveChanges();
        }

        [Fact]
        public async Task TheDeckRanksAStudentWhoIsLookingAboveOneWhoIsNot()
        {
            await using var context = new AppDbContext(_options);
            var matches = await new MatchService(context).GetMatchesAsync(1);

            // Both are still in the deck — switching a course off isn't a block, it just
            // removes that course as a reason to match.
            var looking = matches.Single(m => m.Candidate.StudentId == 2);
            var notLooking = matches.Single(m => m.Candidate.StudentId == 3);

            Assert.True(looking.Score > notLooking.Score);
            Assert.Contains(looking.Reasons, r => r.Kind == MatchReasonKind.SharedCourses);
            Assert.DoesNotContain(notLooking.Reasons, r => r.Kind == MatchReasonKind.SharedCourses);
        }

        public void Dispose() => _connection.Dispose();
    }
}
