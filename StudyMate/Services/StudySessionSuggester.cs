using StudyMate.Models;

namespace StudyMate.Services
{
    /// <summary>
    /// Turns two students' shared availability into concrete "meet then" suggestions.
    ///
    /// Deliberately pure and free of Entity Framework, matching the discipline that
    /// keeps <see cref="MatchScorer"/> directly unit testable. Overlap and shared-course
    /// logic is not reimplemented here — it is delegated to
    /// <see cref="MatchScorer.SharedSlots"/> and <see cref="MatchScorer.SharedCourses"/>,
    /// which already do that job and are already tested. Both students must have their
    /// Enrollments (with Course) and Availability collections populated by the caller.
    /// </summary>
    public static class StudySessionSuggester
    {
        /// <summary>
        /// Ranks the pair's shared free blocks by soonest upcoming occurrence from
        /// <paramref name="today"/>, then Morning before Afternoon before Evening within
        /// the same day, and returns at most <paramref name="max"/> suggestions.
        ///
        /// <paramref name="today"/> is a parameter rather than read from the clock so this
        /// method stays pure and its ranking is directly testable for any day of the week.
        /// Shared courses are attached as context on every suggestion when they exist, but
        /// their absence never suppresses a suggestion — a pair who already accepted each
        /// other should still be told when they are both free.
        /// </summary>
        public static IReadOnlyList<StudySuggestion> Suggest(Student a, Student b, DayOfWeek today, int max = 3)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);

            // MatchScorer.SharedSlots indexes straight off Availability with no null guard
            // (unlike its Enrollments-based helpers, which fall back to Enumerable.Empty).
            // A caller that forgot to eager-load the collection would otherwise surface as
            // a NullReferenceException deep inside borrowed logic; failing soft here instead
            // keeps this method's own contract ("give me suggestions, or none") intact.
            if (a.Availability == null || b.Availability == null || max <= 0)
            {
                return Array.Empty<StudySuggestion>();
            }

            var sharedSlots = MatchScorer.SharedSlots(a, b);
            if (sharedSlots.Count == 0)
            {
                return Array.Empty<StudySuggestion>();
            }

            var sharedCourses = MatchScorer.SharedCourses(a, b);

            return sharedSlots
                .OrderBy(slot => DaysUntil(today, slot.Day))
                .ThenBy(slot => slot.Block)
                .Take(max)
                .Select(slot => new StudySuggestion
                {
                    Day = slot.Day,
                    Block = slot.Block,
                    Courses = sharedCourses,
                    Text = DescribeSuggestion(slot.Day, slot.Block, sharedCourses)
                })
                .ToList();
        }

        /// <summary>
        /// Days from <paramref name="today"/> until <paramref name="day"/> next occurs, in
        /// the range 0-6. Computed modulo 7 (and shifted positive first, since C#'s % keeps
        /// the sign of the dividend) so a day earlier in the week than today still reads as
        /// "coming up soon" instead of negative, and today itself is distance 0.
        /// </summary>
        private static int DaysUntil(DayOfWeek today, DayOfWeek day) =>
            (((int)day - (int)today) % 7 + 7) % 7;

        private static string DescribeSuggestion(DayOfWeek day, TimeBlock block, IReadOnlyList<Course> sharedCourses)
        {
            var when = $"{day} {block.ToString().ToLowerInvariant()}";

            if (sharedCourses.Count == 0)
            {
                return $"You are both free {when}";
            }

            var courseList = string.Join(", ", sharedCourses.Select(c => c.Code));
            return $"You are both free {when} — good time to work on {courseList}";
        }
    }
}
