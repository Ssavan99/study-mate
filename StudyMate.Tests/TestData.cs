using StudyMate.Models;

namespace StudyMate.Tests
{
    /// <summary>Builders that keep the tests readable and independent of database state.</summary>
    internal static class TestData
    {
        public static Course Course(int id, string code) =>
            new() { CourseId = id, Code = code, Title = $"Course {code}", Department = code.Split(' ')[0] };

        public const string DefaultUniversity = "Test University";

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
                University = university,
                Year = 2,
                PreferredNoise = noise,
                Pace = pace,
                PreferredGroupSize = group
            };

        public static Student WithCourses(this Student student, params Course[] courses)
        {
            foreach (var course in courses)
            {
                student.Enrollments.Add(new Enrollment
                {
                    StudentId = student.StudentId,
                    CourseId = course.CourseId,
                    Course = course
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
