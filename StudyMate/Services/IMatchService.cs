using StudyMate.Models;

namespace StudyMate.Services
{
    public interface IMatchService
    {
        /// <summary>
        /// Ranked study-partner candidates for a student, best first. Excludes the student
        /// themselves, anyone already involved in a study request either way, and anyone
        /// the student has passed on.
        /// </summary>
        /// <param name="take">Maximum results to return, or null for all of them.</param>
        Task<IReadOnlyList<MatchResult>> GetMatchesAsync(int studentId, int? take = null, CancellationToken cancellationToken = default);
    }
}
