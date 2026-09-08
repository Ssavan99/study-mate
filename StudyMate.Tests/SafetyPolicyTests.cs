using StudyMate.Models;
using StudyMate.Services;
using Xunit;

namespace StudyMate.Tests;
public class SafetyPolicyTests
{
    [Fact]
    public void BlockIsMutualForVisibility()
    {
        var blocks = new[] { new Block { BlockerStudentId = 1, BlockedStudentId = 2 } };
        Assert.True(SafetyPolicy.IsBlockedPair(1, 2, blocks));
        Assert.True(SafetyPolicy.IsBlockedPair(2, 1, blocks));
        Assert.Contains(2, SafetyPolicy.ExcludedStudentIds(1, blocks));
        Assert.Contains(1, SafetyPolicy.ExcludedStudentIds(2, blocks));
    }
}
