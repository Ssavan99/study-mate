using StudyMate.Services;
using Xunit;

namespace StudyMate.Tests
{
    public class CourseCodeParserTests
    {
        [Theory]
        [InlineData("CSCE 310", "CSCE 310")]
        [InlineData("csce310", "CSCE 310")]
        [InlineData("csce  310", "CSCE 310")]
        [InlineData("  CsCe 310  ", "CSCE 310")]
        [InlineData("MA 1", "MA 1")]
        [InlineData("PSYC 4999", "PSYC 4999")]
        public void ValidCodes_NormalizeToTheSameForm(string input, string expected)
        {
            Assert.Equal(expected, CourseCodeParser.Normalize(input));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("310")]
        [InlineData("CSCE")]
        [InlineData("CSCE31O")]
        [InlineData("TOOLONGDEPT 310")]
        [InlineData("A 310")]
        [InlineData("CSCE 31000")]
        [InlineData("CS-CE 310")]
        [InlineData("<script>alert(1)</script> 310")]
        public void InvalidInput_ReturnsNull(string input)
        {
            Assert.Null(CourseCodeParser.Normalize(input));
        }

        [Fact]
        public void DifferentCasingAndSpacing_NormalizeToTheIdenticalCode()
        {
            // This is the actual product requirement: these must resolve to one course,
            // not three near-duplicates.
            var a = CourseCodeParser.Normalize("CSCE 310");
            var b = CourseCodeParser.Normalize("csce310");
            var c = CourseCodeParser.Normalize("  Csce  310 ");

            Assert.Equal(a, b);
            Assert.Equal(b, c);
        }

        [Fact]
        public void IsValid_AgreesWithNormalize()
        {
            Assert.True(CourseCodeParser.IsValid("CSCE 310"));
            Assert.False(CourseCodeParser.IsValid("not a code"));
        }
    }
}
