using Microsoft.EntityFrameworkCore;
using StudyMate.Data;
using StudyMate.Models;

namespace StudyMate.Services
{
    public class MatchService : IMatchService
    {
        private readonly AppDbContext _db;

        public MatchService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<MatchResult>> GetMatchesAsync(
            int studentId,
            int? take = null,
            CancellationToken cancellationToken = default)
        {
            var viewer = await _db.Students
                .AsNoTracking()
                .Include(s => s.Enrollments).ThenInclude(e => e.Course)
                .Include(s => s.Availability)
                .FirstOrDefaultAsync(s => s.StudentId == studentId, cancellationToken);

            if (viewer == null)
            {
                return Array.Empty<MatchResult>();
            }

            var excludedIds = await ExcludedIdsAsync(studentId, cancellationToken);
            excludedIds.Add(studentId);

            // University is a hard filter, not a scoring input: a cross-university pair
            // never appears in either deck, however well their courses or schedules line up.
            var candidates = await _db.Students
                .AsNoTracking()
                .Where(s => !excludedIds.Contains(s.StudentId) &&
                            viewer.UniversityId.HasValue && s.UniversityId == viewer.UniversityId)
                .Include(s => s.Enrollments).ThenInclude(e => e.Course)
                .Include(s => s.Availability)
                // Two collection includes in one query multiply rows together; split them
                // so a student with 6 courses and 21 slots costs 27 rows, not 126.
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            var ranked = candidates
                .Select(candidate => MatchScorer.Score(viewer, candidate))
                .OrderByDescending(m => m.Score)
                .ThenBy(m => m.Candidate.Name, StringComparer.OrdinalIgnoreCase);

            return take.HasValue ? ranked.Take(take.Value).ToList() : ranked.ToList();
        }

        /// <summary>
        /// Students who should not appear in the deck: anyone already connected to or
        /// awaiting a response from this student, in either direction, plus anyone passed on.
        /// </summary>
        private async Task<HashSet<int>> ExcludedIdsAsync(int studentId, CancellationToken cancellationToken)
        {
            var fromRequests = await _db.StudyRequests
                .AsNoTracking()
                .Where(r => r.FromStudentId == studentId)
                .Select(r => r.ToStudentId)
                .ToListAsync(cancellationToken);

            var toRequests = await _db.StudyRequests
                .AsNoTracking()
                .Where(r => r.ToStudentId == studentId)
                .Select(r => r.FromStudentId)
                .ToListAsync(cancellationToken);

            var passed = await _db.Passes
                .AsNoTracking()
                .Where(p => p.StudentId == studentId)
                .Select(p => p.PassedStudentId)
                .ToListAsync(cancellationToken);

            var excluded = new HashSet<int>(fromRequests);
            excluded.UnionWith(toRequests);
            excluded.UnionWith(passed);
            return excluded;
        }
    }
}
