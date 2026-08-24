using StudyMate.Models;
using Xunit;

namespace StudyMate.Tests
{
    public class MatchTierTests
    {
        [Theory]
        [InlineData(0, MatchTier.None)]
        [InlineData(54, MatchTier.None)]
        [InlineData(55, MatchTier.Strong)]
        [InlineData(74, MatchTier.Strong)]
        [InlineData(75, MatchTier.Great)]
        [InlineData(100, MatchTier.Great)]
        public void ScoreMapsToTheCorrectTierAtEveryBoundary(int score, MatchTier expected)
        {
            Assert.Equal(expected, score.ToTier());
        }

        [Fact]
        public void MatchResultExposesTheTierDerivedFromItsScore()
        {
            var result = new MatchResult { Score = 82 };

            Assert.Equal(MatchTier.Great, result.Tier);
        }
    }
}
