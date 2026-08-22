using StudyMate.Models;

namespace StudyMate.Services
{
    /// <summary>
    /// Scores one student against another and explains the result.
    ///
    /// Deliberately pure and free of Entity Framework so the ranking logic can be
    /// unit tested directly. Both students must have their Enrollments (with Course)
    /// and Availability collections populated by the caller.
    ///
    /// The weighting reflects what actually makes a study partnership work: being in
    /// the same course matters far more than having a similar personality, and being
    /// free at the same time is a hard prerequisite for meeting at all.
    /// </summary>
    public static class MatchScorer
    {
        public const int PointsPerSharedCourse = 25;
        public const int MaxCoursePoints = 60;

        public const int PointsPerSharedSlot = 4;
        public const int MaxAvailabilityPoints = 20;

        public const int MaxNoisePoints = 6;
        public const int MaxPacePoints = 5;
        public const int MaxGroupPoints = 4;
        public const int MaxStylePoints = MaxNoisePoints + MaxPacePoints + MaxGroupPoints;

        public const int SameMajorPoints = 5;

        public const int MaxScore = MaxCoursePoints + MaxAvailabilityPoints + MaxStylePoints + SameMajorPoints;

        public static MatchResult Score(Student viewer, Student candidate)
        {
            ArgumentNullException.ThrowIfNull(viewer);
            ArgumentNullException.ThrowIfNull(candidate);

            var reasons = new List<MatchReason>();

            var sharedCourses = SharedCourses(viewer, candidate);
            var coursePoints = Math.Min(sharedCourses.Count * PointsPerSharedCourse, MaxCoursePoints);
            if (coursePoints > 0)
            {
                reasons.Add(new MatchReason
                {
                    Kind = MatchReasonKind.SharedCourses,
                    Points = coursePoints,
                    Text = DescribeCourses(sharedCourses)
                });
            }

            var sharedSlots = SharedSlots(viewer, candidate);
            var availabilityPoints = Math.Min(sharedSlots.Count * PointsPerSharedSlot, MaxAvailabilityPoints);
            if (availabilityPoints > 0)
            {
                reasons.Add(new MatchReason
                {
                    Kind = MatchReasonKind.Availability,
                    Points = availabilityPoints,
                    Text = DescribeSlots(sharedSlots)
                });
            }

            var stylePoints = StylePoints(viewer, candidate, out var styleText);
            if (stylePoints > 0)
            {
                reasons.Add(new MatchReason
                {
                    Kind = MatchReasonKind.StudyStyle,
                    Points = stylePoints,
                    Text = styleText
                });
            }

            var majorPoints = 0;
            if (!string.IsNullOrWhiteSpace(viewer.Major) &&
                string.Equals(viewer.Major, candidate.Major, StringComparison.OrdinalIgnoreCase))
            {
                majorPoints = SameMajorPoints;
                reasons.Add(new MatchReason
                {
                    Kind = MatchReasonKind.Major,
                    Points = majorPoints,
                    Text = $"Also studying {candidate.Major}"
                });
            }

            return new MatchResult
            {
                Candidate = candidate,
                Score = coursePoints + availabilityPoints + stylePoints + majorPoints,
                Reasons = reasons,
                SharedCourses = sharedCourses,
                SharedSlots = sharedSlots
            };
        }

        public static List<Course> SharedCourses(Student viewer, Student candidate)
        {
            var viewerCourseIds = viewer.Enrollments.Select(e => e.CourseId).ToHashSet();

            return candidate.Enrollments
                .Where(e => viewerCourseIds.Contains(e.CourseId) && e.Course != null)
                .Select(e => e.Course)
                .OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static List<AvailabilitySlot> SharedSlots(Student viewer, Student candidate)
        {
            var viewerSlots = viewer.Availability.Select(a => (a.Day, a.Block)).ToHashSet();

            return candidate.Availability
                .Where(a => viewerSlots.Contains((a.Day, a.Block)))
                .OrderBy(a => a.Day)
                .ThenBy(a => a.Block)
                .ToList();
        }

        /// <summary>
        /// Compatibility across the three style preferences. Adjacent answers score
        /// partial credit — someone who wants silence and someone who wants quiet can
        /// work together; someone who wants silence and someone who wants discussion cannot.
        /// </summary>
        private static int StylePoints(Student viewer, Student candidate, out string text)
        {
            var parts = new List<string>();
            var points = 0;

            var noiseDistance = Math.Abs((int)viewer.PreferredNoise - (int)candidate.PreferredNoise);
            var noisePoints = noiseDistance switch
            {
                0 => MaxNoisePoints,
                1 => MaxNoisePoints / 2,
                _ => 0
            };
            points += noisePoints;
            if (noiseDistance == 0)
            {
                parts.Add(DescribeNoise(candidate.PreferredNoise));
            }
            else if (noiseDistance == 1)
            {
                parts.Add("similar noise preference");
            }

            var paceDistance = Math.Abs((int)viewer.Pace - (int)candidate.Pace);
            var pacePoints = paceDistance switch
            {
                0 => MaxPacePoints,
                1 => 2,
                _ => 0
            };
            points += pacePoints;
            if (paceDistance == 0)
            {
                parts.Add(DescribePace(candidate.Pace));
            }
            else if (paceDistance == 1)
            {
                parts.Add("similar pace");
            }

            var groupCompatible = viewer.PreferredGroupSize == candidate.PreferredGroupSize
                                  || viewer.PreferredGroupSize == GroupSize.Either
                                  || candidate.PreferredGroupSize == GroupSize.Either;
            if (groupCompatible)
            {
                points += MaxGroupPoints;
                if (viewer.PreferredGroupSize == candidate.PreferredGroupSize)
                {
                    parts.Add(DescribeGroup(candidate.PreferredGroupSize));
                }
            }

            text = parts.Count > 0
                ? string.Join(" · ", parts).ToSentenceCase()
                : "Different working styles";

            return points;
        }

        private static string DescribeCourses(IReadOnlyList<Course> courses)
        {
            var label = courses.Count == 1 ? "1 shared course" : $"{courses.Count} shared courses";
            return $"{label}: {string.Join(", ", courses.Select(c => c.Code))}";
        }

        private static string DescribeSlots(IReadOnlyList<AvailabilitySlot> slots)
        {
            var shown = slots.Take(3).Select(FormatSlot).ToList();
            var label = slots.Count == 1 ? "1 shared free block" : $"{slots.Count} shared free blocks";
            var more = slots.Count > shown.Count ? $" +{slots.Count - shown.Count} more" : string.Empty;
            return $"{label}: {string.Join(", ", shown)}{more}";
        }

        private static string FormatSlot(AvailabilitySlot slot) =>
            $"{slot.Day.ToString()[..3]} {slot.Block.ToString().ToLowerInvariant()}";

        private static string DescribeNoise(NoiseLevel level) => level switch
        {
            NoiseLevel.Silent => "both want silence",
            NoiseLevel.Quiet => "both prefer quiet",
            _ => "both like talking it through"
        };

        private static string DescribePace(StudyPace pace) => pace switch
        {
            StudyPace.Steady => "both work steadily",
            StudyPace.Crammer => "both cram before deadlines",
            _ => "both mix it up"
        };

        private static string DescribeGroup(GroupSize size) => size switch
        {
            GroupSize.OneOnOne => "both prefer one-on-one",
            GroupSize.SmallGroup => "both prefer small groups",
            _ => "both flexible on group size"
        };

        private static string ToSentenceCase(this string value) =>
            string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];
    }
}
