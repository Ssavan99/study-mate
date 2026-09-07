using StudyMate.Models;
using StudyMate.Services;
using Xunit;

namespace StudyMate.Tests
{
    public class StudySessionSuggesterTests
    {
        private static readonly Course Algorithms = TestData.Course(1, "CSCE 310");
        private static readonly Course LinearAlgebra = TestData.Course(2, "MATH 314");

        [Fact]
        public void NoOverlap_ReturnsEmpty()
        {
            var a = TestData.Student(1).WithAvailability((DayOfWeek.Monday, TimeBlock.Morning));
            var b = TestData.Student(2).WithAvailability((DayOfWeek.Tuesday, TimeBlock.Evening));

            var suggestions = StudySessionSuggester.Suggest(a, b, DayOfWeek.Monday);

            Assert.Empty(suggestions);
        }

        [Fact]
        public void SingleOverlap_IsSuggested()
        {
            var a = TestData.Student(1).WithAvailability((DayOfWeek.Tuesday, TimeBlock.Evening));
            var b = TestData.Student(2).WithAvailability((DayOfWeek.Tuesday, TimeBlock.Evening));

            var suggestions = StudySessionSuggester.Suggest(a, b, DayOfWeek.Monday);

            var suggestion = Assert.Single(suggestions);
            Assert.Equal(DayOfWeek.Tuesday, suggestion.Day);
            Assert.Equal(TimeBlock.Evening, suggestion.Block);
            Assert.Equal("You are both free Tuesday evening", suggestion.Text);
        }

        [Fact]
        public void MultipleOverlaps_AreRankedBySoonestThenTimeOfDay()
        {
            // From a Monday viewpoint: Wednesday morning is 2 days out, Tuesday evening is
            // 1 day out, and Tuesday morning is also 1 day out but earlier in the day than
            // Tuesday evening. Expected order: Tue morning, Tue evening, Wed morning.
            var a = TestData.Student(1).WithAvailability(
                (DayOfWeek.Wednesday, TimeBlock.Morning),
                (DayOfWeek.Tuesday, TimeBlock.Evening),
                (DayOfWeek.Tuesday, TimeBlock.Morning));
            var b = TestData.Student(2).WithAvailability(
                (DayOfWeek.Wednesday, TimeBlock.Morning),
                (DayOfWeek.Tuesday, TimeBlock.Evening),
                (DayOfWeek.Tuesday, TimeBlock.Morning));

            var suggestions = StudySessionSuggester.Suggest(a, b, DayOfWeek.Monday);

            Assert.Equal(3, suggestions.Count);
            Assert.Equal((DayOfWeek.Tuesday, TimeBlock.Morning), (suggestions[0].Day, suggestions[0].Block));
            Assert.Equal((DayOfWeek.Tuesday, TimeBlock.Evening), (suggestions[1].Day, suggestions[1].Block));
            Assert.Equal((DayOfWeek.Wednesday, TimeBlock.Morning), (suggestions[2].Day, suggestions[2].Block));
        }

        [Fact]
        public void WeekWrapsAround_SoATodayLaterInTheWeekStillRanksAnEarlierDayAsUpcoming()
        {
            // today = Friday, only overlap is Wednesday. Wednesday must not read as "-2 days"
            // (already passed) — it is 5 days out, the next time it occurs.
            var a = TestData.Student(1).WithAvailability((DayOfWeek.Wednesday, TimeBlock.Afternoon));
            var b = TestData.Student(2).WithAvailability((DayOfWeek.Wednesday, TimeBlock.Afternoon));

            var suggestions = StudySessionSuggester.Suggest(a, b, DayOfWeek.Friday);

            var suggestion = Assert.Single(suggestions);
            Assert.Equal(DayOfWeek.Wednesday, suggestion.Day);
        }

        [Fact]
        public void SlotOnTodayItself_RanksAheadOfLaterDays()
        {
            var a = TestData.Student(1).WithAvailability(
                (DayOfWeek.Thursday, TimeBlock.Morning),
                (DayOfWeek.Wednesday, TimeBlock.Evening));
            var b = TestData.Student(2).WithAvailability(
                (DayOfWeek.Thursday, TimeBlock.Morning),
                (DayOfWeek.Wednesday, TimeBlock.Evening));

            var suggestions = StudySessionSuggester.Suggest(a, b, DayOfWeek.Wednesday);

            Assert.Equal(DayOfWeek.Wednesday, suggestions[0].Day);
            Assert.Equal(DayOfWeek.Thursday, suggestions[1].Day);
        }

        [Fact]
        public void OneSidedAvailability_ReturnsEmpty()
        {
            var a = TestData.Student(1).WithAvailability(
                (DayOfWeek.Monday, TimeBlock.Morning),
                (DayOfWeek.Tuesday, TimeBlock.Afternoon));
            var b = TestData.Student(2); // no availability at all

            var suggestions = StudySessionSuggester.Suggest(a, b, DayOfWeek.Monday);

            Assert.Empty(suggestions);
        }

        [Fact]
        public void SharedSlotsWithoutASharedSeekingCourse_StillSuggestsTimes_WithNoCourseClause()
        {
            var a = TestData.Student(1, name: "Alice")
                .WithCourses(Algorithms)
                .WithAvailability((DayOfWeek.Tuesday, TimeBlock.Evening));
            var b = TestData.Student(2, name: "Bea")
                .WithCourses(LinearAlgebra) // no course in common with Alice
                .WithAvailability((DayOfWeek.Tuesday, TimeBlock.Evening));

            var suggestions = StudySessionSuggester.Suggest(a, b, DayOfWeek.Monday);

            var suggestion = Assert.Single(suggestions);
            Assert.Empty(suggestion.Courses);
            Assert.Equal("You are both free Tuesday evening", suggestion.Text);
            Assert.DoesNotContain("CSCE", suggestion.Text);
            Assert.DoesNotContain("MATH", suggestion.Text);
        }

        [Fact]
        public void SharedSlotsWithASharedSeekingCourse_MentionsItInText()
        {
            var a = TestData.Student(1).WithCourses(Algorithms)
                .WithAvailability((DayOfWeek.Tuesday, TimeBlock.Evening));
            var b = TestData.Student(2).WithCourses(Algorithms)
                .WithAvailability((DayOfWeek.Tuesday, TimeBlock.Evening));

            var suggestions = StudySessionSuggester.Suggest(a, b, DayOfWeek.Monday);

            var suggestion = Assert.Single(suggestions);
            Assert.Single(suggestion.Courses);
            Assert.Contains("CSCE 310", suggestion.Text);
        }

        [Fact]
        public void MaxCapsTheNumberOfSuggestions()
        {
            var slots = new[]
            {
                (DayOfWeek.Monday, TimeBlock.Morning),
                (DayOfWeek.Monday, TimeBlock.Afternoon),
                (DayOfWeek.Monday, TimeBlock.Evening),
                (DayOfWeek.Tuesday, TimeBlock.Morning),
                (DayOfWeek.Tuesday, TimeBlock.Afternoon)
            };
            var a = TestData.Student(1).WithAvailability(slots);
            var b = TestData.Student(2).WithAvailability(slots);

            var suggestions = StudySessionSuggester.Suggest(a, b, DayOfWeek.Monday, max: 2);

            Assert.Equal(2, suggestions.Count);
            Assert.Equal(DayOfWeek.Monday, suggestions[0].Day);
            Assert.Equal(TimeBlock.Morning, suggestions[0].Block);
            Assert.Equal(DayOfWeek.Monday, suggestions[1].Day);
            Assert.Equal(TimeBlock.Afternoon, suggestions[1].Block);
        }

        [Fact]
        public void DefaultMax_IsThree()
        {
            var slots = new[]
            {
                (DayOfWeek.Monday, TimeBlock.Morning),
                (DayOfWeek.Monday, TimeBlock.Afternoon),
                (DayOfWeek.Monday, TimeBlock.Evening),
                (DayOfWeek.Tuesday, TimeBlock.Morning)
            };
            var a = TestData.Student(1).WithAvailability(slots);
            var b = TestData.Student(2).WithAvailability(slots);

            var suggestions = StudySessionSuggester.Suggest(a, b, DayOfWeek.Monday);

            Assert.Equal(3, suggestions.Count);
        }

        [Fact]
        public void NullStudentThrows()
        {
            var student = TestData.Student(1);

            Assert.Throws<ArgumentNullException>(() => StudySessionSuggester.Suggest(null, student, DayOfWeek.Monday));
            Assert.Throws<ArgumentNullException>(() => StudySessionSuggester.Suggest(student, null, DayOfWeek.Monday));
        }
    }
}
