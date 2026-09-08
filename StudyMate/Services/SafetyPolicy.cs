using StudyMate.Models;
namespace StudyMate.Services;
public static class SafetyPolicy
{
    public static HashSet<int> ExcludedStudentIds(int id, IEnumerable<Block> blocks) => blocks.Where(b => b.BlockerStudentId == id).Select(b => b.BlockedStudentId).Concat(blocks.Where(b => b.BlockedStudentId == id).Select(b => b.BlockerStudentId)).ToHashSet();
    public static bool IsBlockedPair(int one, int two, IEnumerable<Block> blocks) => ExcludedStudentIds(one, blocks).Contains(two);
}
