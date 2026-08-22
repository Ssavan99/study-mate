using StudyMate.Models;
using StudyMate.Services;
using Xunit;

namespace StudyMate.Tests
{
    public class MatchScorerTests
    {
        private static readonly Course Algorithms = TestData.Course(1, "CSCE 310");
        private static readonly Course LinearAlgebra = TestData.Course(2, "MATH 314");
        private static readonly Course Physics = TestData.Course(3, "PHYS 211");
        private static readonly Course Economics = TestData.Course(4, "ECON 212");

        // --- shared courses -------------------------------------------------

        [Fact]
        public void SharedCourses_AreWorthTwentyFivePointsEach()
        {
            var viewer = TestData.Student(1).WithCourses(Algorithms);
            var candidate = TestData.Student(2).WithCourses(Algorithms);

            var result = MatchScorer.Score(viewer, candidate);

            Assert.Single(result.SharedCourses);
            Assert.Equal(MatchScorer.PointsPerSharedCourse, CoursePoints(result));
        }

        [Fact]
        public void SharedCoursePoints_AreCappedSoOneOverlapCannotDominate()
        {
            var all = new[] { Algorithms, LinearAlgebra, Physics, Economics };
            var viewer = TestData.Student(1).WithCourses(all);
            var candidate = TestData.Student(2).WithCourses(all);

            var result = MatchScorer.Score(viewer, candidate);

            Assert.Equal(4, result.SharedCourses.Count);
            Assert.Equal(MatchScorer.MaxCoursePoints, CoursePoints(result));
        }

        [Fact]
        public void NoSharedCourses_ProducesNoCourseReason()
        {
            var viewer = TestData.Student(1).WithCourses(Algorithms);
            var candidate = TestData.Student(2).WithCourses(Economics);

            var result = MatchScorer.Score(viewer, candidate);

            Assert.Empty(result.SharedCourses);
            Assert.DoesNotContain(result.Reasons, r => r.Kind == MatchReasonKind.SharedCourses);
        }

        [Fact]
        public void MoreSharedCourses_OutranksFewer()
        {
            var viewer = TestData.Student(1).WithCourses(Algorithms, LinearAlgebra);
            var strong = TestData.Student(2).WithCourses(Algorithms, LinearAlgebra);
            var weak = TestData.Student(3).WithCourses(Algorithms);

            Assert.True(MatchScorer.Score(viewer, strong).Score > MatchScorer.Score(viewer, weak).Score);
        }

        [Fact]
        public void SharedCourseReason_NamesTheCourses()
        {
            var viewer = TestData.Student(1).WithCourses(Algorithms, LinearAlgebra);
            var candidate = TestData.Student(2).WithCourses(Algorithms, LinearAlgebra);

            var reason = MatchScorer.Score(viewer, candidate).Reasons
                .Single(r => r.Kind == MatchReasonKind.SharedCourses);

            Assert.Contains("CSCE 310", reason.Text);
            Assert.Contains("MATH 314", reason.Text);
            Assert.Contains("2 shared courses", reason.Text);
        }

        // --- availability ---------------------------------------------------

        [Fact]
        public void OverlappingAvailability_ScoresPerSharedBlock()
        {
            var viewer = TestData.Student(1)
                .WithAvailability((DayOfWeek.Tuesday, TimeBlock.Evening), (DayOfWeek.Thursday, TimeBlock.Evening));
            var candidate = TestData.Student(2)
                .WithAvailability((DayOfWeek.Tuesday, TimeBlock.Evening), (DayOfWeek.Thursday, TimeBlock.Evening));

            var result = MatchScorer.Score(viewer, candidate);

            Assert.Equal(2, result.SharedSlots.Count);
            Assert.Equal(2 * MatchScorer.PointsPerSharedSlot, AvailabilityPoints(result));
        }

        [Fact]
        public void SameDayDifferentBlock_DoesNotCountAsOverlap()
        {
            var viewer = TestData.Student(1).WithAvailability((DayOfWeek.Tuesday, TimeBlock.Morning));
            var candidate = TestData.Student(2).WithAvailability((DayOfWeek.Tuesday, TimeBlock.Evening));

            var result = MatchScorer.Score(viewer, candidate);

            Assert.Empty(result.SharedSlots);
        }

        [Fact]
        public void AvailabilityPoints_AreCapped()
        {
            var many = Enumerable.Range(0, 7)
                .SelectMany(d => new[] { TimeBlock.Morning, TimeBlock.Afternoon, TimeBlock.Evening }
                    .Select(b => ((DayOfWeek)d, b)))
                .ToArray();

            var viewer = TestData.Student(1).WithAvailability(many);
            var candidate = TestData.Student(2).WithAvailability(many);

            Assert.Equal(MatchScorer.MaxAvailabilityPoints, AvailabilityPoints(MatchScorer.Score(viewer, candidate)));
        }

        // --- study style ----------------------------------------------------

        [Fact]
        public void IdenticalStylePreferences_ScoreFullStylePoints()
        {
            var viewer = TestData.Student(1, noise: NoiseLevel.Silent, pace: StudyPace.Steady, group: GroupSize.OneOnOne);
            var candidate = TestData.Student(2, noise: NoiseLevel.Silent, pace: StudyPace.Steady, group: GroupSize.OneOnOne);

            Assert.Equal(MatchScorer.MaxStylePoints, StylePoints(MatchScorer.Score(viewer, candidate)));
        }

        [Fact]
        public void EverySingleStyleDimensionAtItsWorst_ScoresNothing()
        {
            var viewer = TestData.Student(1, noise: NoiseLevel.Silent, pace: StudyPace.Steady, group: GroupSize.OneOnOne);
            var candidate = TestData.Student(2, noise: NoiseLevel.Discussion, pace: StudyPace.Crammer, group: GroupSize.SmallGroup);

            var result = MatchScorer.Score(viewer, candidate);

            Assert.Equal(0, StylePoints(result));
            // No reason is emitted at all when a dimension group scores nothing, so assert
            // the absence explicitly rather than relying on an empty sum.
            Assert.DoesNotContain(result.Reasons, r => r.Kind == MatchReasonKind.StudyStyle);
        }

        // Each of the following holds two dimensions identical so the third is isolated,
        // which pins the exact partial-credit tier rather than just "somewhere in between".

        [Theory]
        [InlineData(NoiseLevel.Silent, MatchScorer.MaxNoisePoints)]                 // identical
        [InlineData(NoiseLevel.Quiet, MatchScorer.MaxNoisePoints / 2)]              // adjacent
        [InlineData(NoiseLevel.Discussion, 0)]                                      // opposite
        public void NoiseDimension_ScoresByDistance(NoiseLevel candidateNoise, int expectedNoisePoints)
        {
            var viewer = TestData.Student(1, noise: NoiseLevel.Silent, pace: StudyPace.Steady, group: GroupSize.OneOnOne);
            var candidate = TestData.Student(2, noise: candidateNoise, pace: StudyPace.Steady, group: GroupSize.OneOnOne);

            var expected = expectedNoisePoints + MatchScorer.MaxPacePoints + MatchScorer.MaxGroupPoints;

            Assert.Equal(expected, StylePoints(MatchScorer.Score(viewer, candidate)));
        }

        [Theory]
        [InlineData(StudyPace.Steady, MatchScorer.MaxPacePoints)]
        [InlineData(StudyPace.Mixed, MatchScorer.MaxPacePoints / 2)]
        [InlineData(StudyPace.Crammer, 0)]
        public void PaceDimension_ScoresByDistance(StudyPace candidatePace, int expectedPacePoints)
        {
            var viewer = TestData.Student(1, noise: NoiseLevel.Quiet, pace: StudyPace.Steady, group: GroupSize.OneOnOne);
            var candidate = TestData.Student(2, noise: NoiseLevel.Quiet, pace: candidatePace, group: GroupSize.OneOnOne);

            var expected = MatchScorer.MaxNoisePoints + expectedPacePoints + MatchScorer.MaxGroupPoints;

            Assert.Equal(expected, StylePoints(MatchScorer.Score(viewer, candidate)));
        }

        [Theory]
        [InlineData(GroupSize.Either, GroupSize.OneOnOne, true)]
        [InlineData(GroupSize.Either, GroupSize.SmallGroup, true)]
        [InlineData(GroupSize.Either, GroupSize.Either, true)]
        [InlineData(GroupSize.OneOnOne, GroupSize.OneOnOne, true)]
        [InlineData(GroupSize.OneOnOne, GroupSize.SmallGroup, false)]
        public void GroupSizeDimension_AwardsPointsOnlyWhenCompatible(GroupSize viewerSize, GroupSize candidateSize, bool compatible)
        {
            var viewer = TestData.Student(1, noise: NoiseLevel.Quiet, pace: StudyPace.Steady, group: viewerSize);
            var candidate = TestData.Student(2, noise: NoiseLevel.Quiet, pace: StudyPace.Steady, group: candidateSize);

            var expected = MatchScorer.MaxNoisePoints + MatchScorer.MaxPacePoints
                           + (compatible ? MatchScorer.MaxGroupPoints : 0);

            Assert.Equal(expected, StylePoints(MatchScorer.Score(viewer, candidate)));
        }

        [Fact]
        public void AFlexibleGroupSizeStillEarnsAWrittenReason()
        {
            // Only the group dimension scores here, and it scores because one side is
            // flexible rather than because both agree. The text must still say something.
            var viewer = TestData.Student(1, noise: NoiseLevel.Silent, pace: StudyPace.Steady, group: GroupSize.Either);
            var candidate = TestData.Student(2, noise: NoiseLevel.Discussion, pace: StudyPace.Crammer, group: GroupSize.OneOnOne);

            var reason = MatchScorer.Score(viewer, candidate).Reasons
                .Single(r => r.Kind == MatchReasonKind.StudyStyle);

            Assert.Equal(MatchScorer.MaxGroupPoints, reason.Points);
            Assert.NotEqual("Different working styles", reason.Text);
            Assert.Contains("group size", reason.Text, StringComparison.OrdinalIgnoreCase);
        }

        // --- major ----------------------------------------------------------

        [Fact]
        public void SameMajor_AddsABonus()
        {
            var viewer = TestData.Student(1, major: "Computer Science");
            var same = TestData.Student(2, major: "Computer Science");
            var different = TestData.Student(3, major: "Economics");

            Assert.Equal(
                MatchScorer.SameMajorPoints,
                MatchScorer.Score(viewer, same).Score - MatchScorer.Score(viewer, different).Score);
        }

        [Fact]
        public void MajorComparison_IgnoresCase()
        {
            var viewer = TestData.Student(1, major: "Computer Science");
            var candidate = TestData.Student(2, major: "computer science");

            Assert.Contains(MatchScorer.Score(viewer, candidate).Reasons, r => r.Kind == MatchReasonKind.Major);
        }

        [Fact]
        public void BlankMajor_DoesNotCountAsAMatch()
        {
            var viewer = TestData.Student(1, major: "");
            var candidate = TestData.Student(2, major: "");

            Assert.DoesNotContain(MatchScorer.Score(viewer, candidate).Reasons, r => r.Kind == MatchReasonKind.Major);
        }

        // --- totals and edges -----------------------------------------------

        [Fact]
        public void APerfectMatch_ScoresExactlyOneHundred()
        {
            var courses = new[] { Algorithms, LinearAlgebra, Physics };
            var slots = new[]
            {
                (DayOfWeek.Monday, TimeBlock.Evening),
                (DayOfWeek.Tuesday, TimeBlock.Evening),
                (DayOfWeek.Wednesday, TimeBlock.Evening),
                (DayOfWeek.Thursday, TimeBlock.Evening),
                (DayOfWeek.Friday, TimeBlock.Evening)
            };

            var viewer = TestData.Student(1, noise: NoiseLevel.Quiet, pace: StudyPace.Steady, group: GroupSize.OneOnOne)
                .WithCourses(courses).WithAvailability(slots);
            var candidate = TestData.Student(2, noise: NoiseLevel.Quiet, pace: StudyPace.Steady, group: GroupSize.OneOnOne)
                .WithCourses(courses).WithAvailability(slots);

            var result = MatchScorer.Score(viewer, candidate);

            Assert.Equal(MatchScorer.MaxScore, result.Score);
            Assert.Equal(100, MatchScorer.MaxScore);
        }

        [Fact]
        public void ScoreNeverExceedsTheMaximum()
        {
            var courses = Enumerable.Range(1, 10).Select(i => TestData.Course(i, $"DEPT {i}00")).ToArray();
            var slots = Enumerable.Range(0, 7)
                .SelectMany(d => new[] { TimeBlock.Morning, TimeBlock.Afternoon, TimeBlock.Evening }
                    .Select(b => ((DayOfWeek)d, b)))
                .ToArray();

            var viewer = TestData.Student(1).WithCourses(courses).WithAvailability(slots);
            var candidate = TestData.Student(2).WithCourses(courses).WithAvailability(slots);

            Assert.True(MatchScorer.Score(viewer, candidate).Score <= MatchScorer.MaxScore);
        }

        [Fact]
        public void StudentsWithNoCoursesOrAvailability_StillScoreWithoutThrowing()
        {
            var viewer = TestData.Student(1);
            var candidate = TestData.Student(2);

            var result = MatchScorer.Score(viewer, candidate);

            Assert.True(result.Score > 0);
            Assert.Empty(result.SharedCourses);
            Assert.Empty(result.SharedSlots);
        }

        [Fact]
        public void EveryReasonCarriesPositivePointsAndText()
        {
            var viewer = TestData.Student(1).WithCourses(Algorithms).WithAvailability((DayOfWeek.Monday, TimeBlock.Evening));
            var candidate = TestData.Student(2).WithCourses(Algorithms).WithAvailability((DayOfWeek.Monday, TimeBlock.Evening));

            var result = MatchScorer.Score(viewer, candidate);

            Assert.NotEmpty(result.Reasons);
            Assert.All(result.Reasons, r =>
            {
                Assert.True(r.Points > 0);
                Assert.False(string.IsNullOrWhiteSpace(r.Text));
            });
        }

        [Fact]
        public void TheReasonsAlwaysAddUpToTheScore()
        {
            var viewer = TestData.Student(1, major: "Physics", noise: NoiseLevel.Quiet)
                .WithCourses(Algorithms, Physics)
                .WithAvailability((DayOfWeek.Monday, TimeBlock.Morning), (DayOfWeek.Friday, TimeBlock.Afternoon));
            var candidate = TestData.Student(2, major: "Physics", noise: NoiseLevel.Quiet)
                .WithCourses(Physics, Economics)
                .WithAvailability((DayOfWeek.Friday, TimeBlock.Afternoon));

            var result = MatchScorer.Score(viewer, candidate);

            Assert.Equal(result.Score, result.Reasons.Sum(r => r.Points));
        }

        [Fact]
        public void ScoringIsSymmetric()
        {
            var viewer = TestData.Student(1, major: "Physics", noise: NoiseLevel.Silent, group: GroupSize.OneOnOne)
                .WithCourses(Algorithms).WithAvailability((DayOfWeek.Monday, TimeBlock.Morning));
            var candidate = TestData.Student(2, major: "Economics", noise: NoiseLevel.Quiet, group: GroupSize.Either)
                .WithCourses(Algorithms, Physics).WithAvailability((DayOfWeek.Monday, TimeBlock.Morning));

            Assert.Equal(MatchScorer.Score(viewer, candidate).Score, MatchScorer.Score(candidate, viewer).Score);
        }

        [Fact]
        public void UnloadedCourseNavigation_ThrowsInsteadOfSilentlyScoringLower()
        {
            // Simulates a caller that included Enrollments but forgot ThenInclude(e => e.Course).
            // Scoring off the loaded objects would quietly drop 25 points; it must fail loudly.
            var viewer = TestData.Student(1).WithCourses(Algorithms);
            var candidate = TestData.Student(2);
            candidate.Enrollments.Add(new Enrollment { StudentId = 2, CourseId = Algorithms.CourseId, Course = null });

            var ex = Assert.Throws<InvalidOperationException>(() => MatchScorer.Score(viewer, candidate));
            Assert.Contains("ThenInclude", ex.Message);
        }

        [Fact]
        public void EveryAwardedPointHasAnExplanation()
        {
            // Exhaustive over the style dimensions: no combination may score points
            // while leaving the reason text as the "nothing matched" fallback.
            foreach (NoiseLevel vn in Enum.GetValues<NoiseLevel>())
            foreach (StudyPace vp in Enum.GetValues<StudyPace>())
            foreach (GroupSize vg in Enum.GetValues<GroupSize>())
            foreach (NoiseLevel cn in Enum.GetValues<NoiseLevel>())
            foreach (StudyPace cp in Enum.GetValues<StudyPace>())
            foreach (GroupSize cg in Enum.GetValues<GroupSize>())
            {
                var viewer = TestData.Student(1, noise: vn, pace: vp, group: vg);
                var candidate = TestData.Student(2, noise: cn, pace: cp, group: cg);

                var result = MatchScorer.Score(viewer, candidate);

                Assert.Equal(result.Score, result.Reasons.Sum(r => r.Points));
                Assert.All(result.Reasons, r => Assert.False(string.IsNullOrWhiteSpace(r.Text)));

                var style = result.Reasons.SingleOrDefault(r => r.Kind == MatchReasonKind.StudyStyle);
                if (style != null)
                {
                    Assert.NotEqual("Different working styles", style.Text);
                }
            }
        }

        [Fact]
        public void ScoringANullStudentThrows()
        {
            var student = TestData.Student(1);

            Assert.Throws<ArgumentNullException>(() => MatchScorer.Score(null, student));
            Assert.Throws<ArgumentNullException>(() => MatchScorer.Score(student, null));
        }

        private static int CoursePoints(MatchResult r) => PointsFor(r, MatchReasonKind.SharedCourses);
        private static int AvailabilityPoints(MatchResult r) => PointsFor(r, MatchReasonKind.Availability);
        private static int StylePoints(MatchResult r) => PointsFor(r, MatchReasonKind.StudyStyle);

        private static int PointsFor(MatchResult result, MatchReasonKind kind) =>
            result.Reasons.Where(r => r.Kind == kind).Sum(r => r.Points);
    }
}
