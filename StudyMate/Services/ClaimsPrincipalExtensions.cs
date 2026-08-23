using System.Security.Claims;

namespace StudyMate.Services
{
    public static class ClaimsPrincipalExtensions
    {
        /// <summary>The signed-in student's id, or null if the request is anonymous.</summary>
        public static int? GetStudentId(this ClaimsPrincipal principal)
        {
            var value = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(value, out var id) ? id : null;
        }
    }
}
