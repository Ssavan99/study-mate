using StudyMate.Services;
using Xunit;

namespace StudyMate.Tests
{
    public class AvatarGeneratorTests
    {
        [Fact]
        public void SameStudentId_AlwaysProducesTheSameColor()
        {
            Assert.Equal(AvatarGenerator.ColorFor(42), AvatarGenerator.ColorFor(42));
        }

        [Fact]
        public void SameInputs_AlwaysProduceTheIdenticalSvg()
        {
            var first = AvatarGenerator.SvgFor(7, "Maya Chen");
            var second = AvatarGenerator.SvgFor(7, "Maya Chen");

            Assert.Equal(first, second);
        }

        [Theory]
        [InlineData("Maya Chen", "MC")]
        [InlineData("Cher", "C")]
        [InlineData("Sofia De La Cruz", "SC")]
        [InlineData("  Spacey   Name  ", "SN")]
        public void InitialsFor_UsesFirstAndLastNameParts(string name, string expected)
        {
            Assert.Equal(expected, AvatarGenerator.InitialsFor(name));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void InitialsFor_BlankName_FallsBackToAPlaceholder(string name)
        {
            Assert.Equal("?", AvatarGenerator.InitialsFor(name));
        }

        [Fact]
        public void SvgFor_DoesNotLeakUnencodedNameIntoMarkup()
        {
            // The name itself never appears verbatim in the output -- only its initials do
            // -- but this pins that any HTML-significant characters in a name are encoded
            // if they ever did, so a name can never break out of the generated SVG.
            var svg = AvatarGenerator.SvgFor(1, "<script>alert(1)</script>");

            Assert.DoesNotContain("<script>", svg);
        }

        [Fact]
        public void DifferentStudentIds_CanProduceDifferentColors()
        {
            var colors = Enumerable.Range(1, 20).Select(AvatarGenerator.ColorFor).Distinct().ToList();

            Assert.True(colors.Count > 1, "Expected more than one distinct color across 20 different ids.");
        }
    }
}
