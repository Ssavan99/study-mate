using StudyMate.Models;

namespace StudyMate.Tests
{
    /// <summary>Builders that keep the tests readable and independent of database state.</summary>
    internal static class TestData
    {
        public static Course Course(int id, string code, string university = DefaultUniversity) =>
            new()
            {
                CourseId = id,
                Code = code,
                Title = $"Course {code}",
                Department = code.Split(' ')[0],
                UniversityId = UniversityIdFor(university)
            };

        public const string DefaultUniversity = "Test University";

        public static IEnumerable<University> Universities() => new[]
        {
            new University { UniversityId = 1, Name = DefaultUniversity, Slug = "test-university" },
            new University { UniversityId = 2, Name = "A Different University", Slug = "different-university" }
        };

        public static Student Student(
            int id,
            string name = "Test Student",
            string major = "Computer Science",
            NoiseLevel noise = NoiseLevel.Quiet,
            StudyPace pace = StudyPace.Mixed,
            GroupSize group = GroupSize.Either,
            string university = DefaultUniversity) =>
            new()
            {
                StudentId = id,
                Name = name,
                Email = $"student{id}@example.edu",
                PasswordHash = "not-a-real-hash",
                Major = major,
                UniversityId = UniversityIdFor(university),
                Year = 2,
                PreferredNoise = noise,
                Pace = pace,
                PreferredGroupSize = group
            };

        private static int UniversityIdFor(string university) =>
            string.Equals(university, DefaultUniversity, StringComparison.OrdinalIgnoreCase) ? 1 : 2;

        /// <summary>Enrols the student and flags every course as seeking a partner.</summary>
        public static Student WithCourses(this Student student, params Course[] courses)
        {
            foreach (var course in courses)
            {
                student.Enrollments.Add(new Enrollment
                {
                    StudentId = student.StudentId,
                    CourseId = course.CourseId,
                    Course = course,
                    SeekingPartner = true
                });
            }

            return student;
        }

        /// <summary>Enrols the student without flagging the course — on the schedule, not looking.</summary>
        public static Student WithCoursesNotSeeking(this Student student, params Course[] courses)
        {
            foreach (var course in courses)
            {
                student.Enrollments.Add(new Enrollment
                {
                    StudentId = student.StudentId,
                    CourseId = course.CourseId,
                    Course = course,
                    SeekingPartner = false
                });
            }

            return student;
        }

        public static Student WithAvailability(this Student student, params (DayOfWeek Day, TimeBlock Block)[] slots)
        {
            foreach (var (day, block) in slots)
            {
                student.Availability.Add(new AvailabilitySlot
                {
                    StudentId = student.StudentId,
                    Day = day,
                    Block = block
                });
            }

            return student;
        }
    }
}
