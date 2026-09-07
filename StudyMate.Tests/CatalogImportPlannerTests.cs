using StudyMate.Models;
using StudyMate.Services;
using Xunit;

namespace StudyMate.Tests
{
    public class CatalogImportPlannerTests
    {
        [Fact]
        public void MissingCourses_ReusesExistingCodesWithoutCaseSensitiveDuplicates()
        {
            var catalog = new[]
            {
                new CatalogCourse("CSCE 310", "Data Structures and Algorithms", "CSCE"),
                new CatalogCourse("MATH 314", "Linear Algebra", "MATH")
            };
            var existing = new[] { new Course { Code = "csce 310", Title = "Old title", Department = "CSCE", UniversityId = 1 } };

            var missing = CatalogImportPlanner.MissingCourses(catalog, existing);

            Assert.Single(missing);
            Assert.Equal("MATH 314", missing[0].Code);
        }
    }
}
