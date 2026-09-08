using StudyMate.Models;
using StudyMate.Services;
using Xunit;

namespace StudyMate.Tests
{
    public class UniversityIdentityTests
    {
        [Theory]
        [InlineData(" University   of\tNebraska ", "university of nebraska")]
        [InlineData("IOWA STATE UNIVERSITY", "iowa state university")]
        public void NormalizeName_TrimsFoldsCaseAndCollapsesWhitespace(string input, string expected)
        {
            Assert.Equal(expected, UniversityIdentity.NormalizeName(input));
        }

        [Fact]
        public void ResolveVerifiedEmail_IsCaseInsensitiveAndAcceptsRealSubdomains()
        {
            var university = new University { UniversityId = 7, Name = "University of Nebraska–Lincoln", Slug = "unl", IsActive = true };
            var result = UniversityIdentity.ResolveVerifiedEmail("Student@CS.UNL.EDU", true,
                new[] { new UniversityEmailDomain { Domain = "unl.edu", University = university } });

            Assert.Same(university, result);
        }

        [Fact]
        public void ResolveVerifiedEmail_RejectsUnverifiedAndLookalikeAddresses()
        {
            var university = new University { UniversityId = 7, Name = "University", Slug = "unl", IsActive = true };
            var domains = new[] { new UniversityEmailDomain { Domain = "unl.edu", University = university } };

            Assert.Null(UniversityIdentity.ResolveVerifiedEmail("student@unl.edu", false, domains));
            Assert.Null(UniversityIdentity.ResolveVerifiedEmail("student@evilunl.edu", true, domains));
        }
    }
}
