using System.Text.RegularExpressions;

namespace StudyMate.Services
{
    /// <summary>
    /// Validates and normalizes a student-entered course code. There is no free catalog
    /// API to check a course is real against, so this cannot verify legitimacy — what it
    /// does is block obvious garbage and normalize casing/spacing so "CSCE310",
    /// "CSCE 310" and "csce  310" all resolve to the same course rather than fragmenting
    /// into near-duplicates.
    /// </summary>
    public static partial class CourseCodeParser
    {
        [GeneratedRegex(@"^\s*([A-Za-z]{2,6}(?:\s+[A-Za-z]{1,6})?)\s*(\d{1,4}[Hh]?)\s*$")]
        private static partial Regex Pattern();

        /// <summary>
        /// Returns the normalized "DEPT ###" form (uppercase department, single space,
        /// number unchanged) or null if the input doesn't match a department-code shape.
        /// </summary>
        public static string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            var match = Pattern().Match(input);
            if (!match.Success)
            {
                return null;
            }

            var department = Regex.Replace(match.Groups[1].Value.Trim(), @"\s+", " ").ToUpperInvariant();
            return $"{department} {match.Groups[2].Value}";
        }

        public static bool IsValid(string input) => Normalize(input) != null;
    }
}
