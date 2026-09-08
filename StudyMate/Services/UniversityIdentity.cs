using System.Text.RegularExpressions;
using StudyMate.Models;

namespace StudyMate.Services
{
    /// <summary>Pure identity rules shared by migration, seeding, and external sign-in.</summary>
    public static class UniversityIdentity
    {
        public static string NormalizeName(string name) =>
            Regex.Replace((name ?? string.Empty).Trim(), @"\s+", " ").ToLowerInvariant();

        public static string Slugify(string name)
        {
            var normalized = NormalizeName(name);
            var slug = Regex.Replace(normalized, @"[^a-z0-9]+", "-").Trim('-');
            return string.IsNullOrWhiteSpace(slug) ? "university" : slug;
        }

        /// <summary>
        /// Resolves only an address the provider explicitly marked verified. A registered
        /// domain also covers its DNS subdomains, but never a lookalike suffix.
        /// </summary>
        public static University ResolveVerifiedEmail(
            string email,
            bool emailVerified,
            IEnumerable<UniversityEmailDomain> domains)
        {
            if (!emailVerified || string.IsNullOrWhiteSpace(email)) return null;

            var at = email.Trim().LastIndexOf('@');
            if (at <= 0 || at == email.Length - 1) return null;
            var emailDomain = email[(at + 1)..].Trim().TrimEnd('.').ToLowerInvariant();

            return domains
                .Where(d => d.University?.IsActive == true && !string.IsNullOrWhiteSpace(d.Domain))
                .OrderByDescending(d => d.Domain.Length)
                .FirstOrDefault(d => IsDomainOrSubdomain(emailDomain, d.Domain))
                ?.University;
        }

        private static bool IsDomainOrSubdomain(string emailDomain, string registeredDomain)
        {
            var normalized = registeredDomain.Trim().TrimEnd('.').ToLowerInvariant();
            return emailDomain == normalized || emailDomain.EndsWith($".{normalized}", StringComparison.Ordinal);
        }
    }
}
