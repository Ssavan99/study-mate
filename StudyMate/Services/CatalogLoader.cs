using System.Text.Json;
using StudyMate.Models;

namespace StudyMate.Services
{
    public sealed record CatalogCourse(string Code, string Title, string Department);

    /// <summary>Reads checked-in catalogs; it intentionally has no network dependency.</summary>
    public static class CatalogLoader
    {
        public static IReadOnlyList<CatalogCourse> Load(string path)
        {
            var catalog = JsonSerializer.Deserialize<CatalogDocument>(File.ReadAllText(path), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new CatalogDocument();

            return catalog.Departments
                .Where(d => !string.IsNullOrWhiteSpace(d.Code))
                .SelectMany(d => d.Courses.Select(c => new CatalogCourse(
                    CourseCodeParser.Normalize(c.Code) ?? c.Code.Trim().ToUpperInvariant(),
                    c.Title?.Trim() ?? string.Empty,
                    d.Code.Trim().ToUpperInvariant())))
                .Where(c => !string.IsNullOrWhiteSpace(c.Code) && !string.IsNullOrWhiteSpace(c.Title))
                .DistinctBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private sealed class CatalogDocument
        {
            public List<CatalogDepartment> Departments { get; set; } = new();
        }

        private sealed class CatalogDepartment
        {
            public string Code { get; set; }
            public List<CatalogCourseDocument> Courses { get; set; } = new();
        }

        private sealed class CatalogCourseDocument
        {
            public string Code { get; set; }
            public string Title { get; set; }
        }
    }

    /// <summary>Pure duplicate-prevention rule used before catalog rows are persisted.</summary>
    public static class CatalogImportPlanner
    {
        public static IReadOnlyList<CatalogCourse> MissingCourses(
            IEnumerable<CatalogCourse> catalog,
            IEnumerable<Course> existingCourses)
        {
            var existingCodes = existingCourses.Select(c => c.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return catalog.Where(c => !existingCodes.Contains(c.Code)).ToList();
        }
    }
}
