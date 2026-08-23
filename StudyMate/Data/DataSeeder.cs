using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudyMate.Models;

namespace StudyMate.Data
{
    /// <summary>
    /// Populates the database with fictional students for the demonstration deployment.
    ///
    /// The hosting filesystem is ephemeral, so this runs on every start and the data set
    /// is rebuilt from scratch each time. It is deterministic — a fixed random seed — so
    /// the deployed demo looks the same on every restart and screenshots stay accurate.
    ///
    /// None of these people are real.
    /// </summary>
    public static class DataSeeder
    {
        private const int RandomSeed = 20260822;

        /// <summary>
        /// Assumes a single instance owns the database. That holds on the free tier, where
        /// each container has its own ephemeral SQLite file — two runners would both pass
        /// the emptiness check and collide on the unique indexes.
        /// </summary>
        public static async Task SeedAsync(AppDbContext db, IPasswordHasher<Student> passwordHasher)
        {
            if (await db.Students.AnyAsync())
            {
                return;
            }

            // Students are saved before their enrolments, so a crash between the two would
            // leave the emptiness check above satisfied by students who have no courses and
            // no availability. Every match would then score near zero and the demonstration
            // would look broken rather than failed. One transaction makes it all or nothing.
            await using var transaction = await db.Database.BeginTransactionAsync();

            var random = new Random(RandomSeed);

            var courses = BuildCourses();
            db.Courses.AddRange(courses);
            await db.SaveChangesAsync();

            var coursesByDepartment = courses
                .GroupBy(c => c.Department)
                .ToDictionary(g => g.Key, g => g.ToList());

            var students = new List<Student>();
            students.AddRange(BuildPersonas(passwordHasher));
            students.AddRange(BuildCrowd(random, passwordHasher));

            db.Students.AddRange(students);
            await db.SaveChangesAsync();

            foreach (var student in students)
            {
                AssignCourses(db, student, coursesByDepartment, courses, random);
                AssignAvailability(db, student, random);
            }

            await db.SaveChangesAsync();
            await SeedRequestsAsync(db, students, random);

            await transaction.CommitAsync();
        }

        // --- courses --------------------------------------------------------

        private static List<Course> BuildCourses() => new()
        {
            new Course { Code = "CSCE 155", Title = "Computer Science I", Department = "Computer Science" },
            new Course { Code = "CSCE 156", Title = "Computer Science II", Department = "Computer Science" },
            new Course { Code = "CSCE 235", Title = "Discrete Mathematics", Department = "Computer Science" },
            new Course { Code = "CSCE 310", Title = "Data Structures and Algorithms", Department = "Computer Science" },
            new Course { Code = "CSCE 361", Title = "Software Engineering", Department = "Computer Science" },
            new Course { Code = "CSCE 411", Title = "Operating Systems", Department = "Computer Science" },
            new Course { Code = "CSCE 478", Title = "Machine Learning", Department = "Computer Science" },

            new Course { Code = "MATH 106", Title = "Calculus I", Department = "Mathematics" },
            new Course { Code = "MATH 107", Title = "Calculus II", Department = "Mathematics" },
            new Course { Code = "MATH 208", Title = "Calculus III", Department = "Mathematics" },
            new Course { Code = "MATH 314", Title = "Linear Algebra", Department = "Mathematics" },
            new Course { Code = "MATH 380", Title = "Statistics and Probability", Department = "Mathematics" },

            new Course { Code = "PHYS 211", Title = "General Physics I", Department = "Physics" },
            new Course { Code = "PHYS 212", Title = "General Physics II", Department = "Physics" },
            new Course { Code = "PHYS 361", Title = "Classical Mechanics", Department = "Physics" },

            new Course { Code = "BIOL 101", Title = "General Biology", Department = "Biology" },
            new Course { Code = "BIOL 206", Title = "Genetics", Department = "Biology" },
            new Course { Code = "BIOL 312", Title = "Cell Biology", Department = "Biology" },

            new Course { Code = "CHEM 109", Title = "General Chemistry", Department = "Chemistry" },
            new Course { Code = "CHEM 251", Title = "Organic Chemistry", Department = "Chemistry" },

            new Course { Code = "ECON 211", Title = "Microeconomics", Department = "Economics" },
            new Course { Code = "ECON 212", Title = "Macroeconomics", Department = "Economics" },
            new Course { Code = "ECON 417", Title = "Econometrics", Department = "Economics" },

            new Course { Code = "PSYC 181", Title = "Introduction to Psychology", Department = "Psychology" },
            new Course { Code = "PSYC 350", Title = "Research Methods", Department = "Psychology" }
        };

        // --- the four demo personas ------------------------------------------

        /// <summary>
        /// Hand-written so the landing page has recognisable, distinct entry points.
        /// Their majors are the most common ones in the crowd, which guarantees each
        /// of them opens onto a populated deck rather than an empty one.
        /// </summary>
        private static List<Student> BuildPersonas(IPasswordHasher<Student> passwordHasher) => new()
        {
            NewStudent(passwordHasher, "Maya Chen", "maya.chen@example.edu", "Computer Science", 3,
                "Third year CS, deep in algorithms. I like working through problem sets out loud with someone.",
                NoiseLevel.Discussion, StudyPace.Steady, GroupSize.OneOnOne, isDemo: true),

            NewStudent(passwordHasher, "Daniel Okafor", "daniel.okafor@example.edu", "Mathematics", 2,
                "Maths major who mostly needs someone to sit with in the library and stay off my phone.",
                NoiseLevel.Silent, StudyPace.Steady, GroupSize.OneOnOne, isDemo: true),

            NewStudent(passwordHasher, "Priya Raman", "priya.raman@example.edu", "Computer Science", 4,
                "Final year, juggling a capstone. Realistically I study in bursts before deadlines.",
                NoiseLevel.Quiet, StudyPace.Crammer, GroupSize.SmallGroup, isDemo: true),

            NewStudent(passwordHasher, "Sofia Duarte", "sofia.duarte@example.edu", "Biology", 2,
                "Pre-med, so a lot of memorisation. Happy to quiz people if they quiz me back.",
                NoiseLevel.Quiet, StudyPace.Mixed, GroupSize.Either, isDemo: true)
        };

        // --- the surrounding crowd -------------------------------------------

        private static readonly string[] FirstNames =
        {
            "Aisha", "Liam", "Noor", "Ethan", "Camila", "Jonas", "Mei", "Tobias", "Fatima", "Oscar",
            "Ines", "Rahul", "Elena", "Marcus", "Yuki", "Adam", "Zara", "Felix", "Nadia", "Theo",
            "Amara", "Julian", "Leila", "Victor", "Hana", "Samuel", "Rosa", "Idris", "Clara", "Nikolai",
            "Bianca", "Omar", "Freya", "Andres", "Simone", "Kwame", "Anya", "Mateo", "Delphine", "Ravi",
            "Greta", "Salim", "Lucia", "Anton", "Naomi", "Petros", "Iris", "Hugo", "Talia", "Emre",
            "Marta", "Caleb", "Sana", "Bruno", "Wren", "Diego"
        };

        private static readonly string[] LastNames =
        {
            "Whitfield", "Baros", "Lindqvist", "Achebe", "Moreau", "Tanaka", "Kowalski", "Ferreira",
            "Haddad", "Nyberg", "Almeida", "Bergstrom", "Costa", "Draper", "Eriksen", "Farrell",
            "Gallagher", "Halvorsen", "Ivanov", "Jozwiak", "Kaminski", "Larsen", "Mbeki", "Nakamura",
            "Oyelaran", "Petrov", "Quintero", "Rasmussen", "Silva", "Toureille", "Ustinov", "Varga",
            "Wachowski", "Xiang", "Yilmaz", "Zabala", "Brennan", "Castellanos", "Dubois", "Engberg",
            "Fontaine", "Grimaldi", "Holloway", "Iversen", "Jansen", "Kohler", "Lombardi", "Mazur",
            "Novak", "Okonkwo", "Pahlavi", "Renard", "Strand", "Thorne", "Ulriksen", "Voss"
        };

        private static readonly (string Major, string Department)[] Majors =
        {
            ("Computer Science", "Computer Science"),
            ("Computer Science", "Computer Science"),
            ("Computer Science", "Computer Science"),
            ("Mathematics", "Mathematics"),
            ("Mathematics", "Mathematics"),
            ("Physics", "Physics"),
            ("Biology", "Biology"),
            ("Biology", "Biology"),
            ("Chemistry", "Chemistry"),
            ("Economics", "Economics"),
            ("Economics", "Economics"),
            ("Psychology", "Psychology")
        };

        private static readonly string[] Bios =
        {
            "Usually in the science library between lectures.",
            "Looking for someone to keep me accountable this term.",
            "I explain things better when I have to say them out loud.",
            "Happy to work through past papers together.",
            "Commuter student, so weekday afternoons work best for me.",
            "I take good notes and I am happy to share them.",
            "Trying to stop leaving everything until the last week.",
            "Prefer a set weekly time rather than ad hoc sessions.",
            "Second time taking one of these, so I know where the traps are.",
            "Coffee first, then three solid hours.",
            "I like short focused sessions rather than long ones.",
            "Quiet worker, but ask me anything and I will help.",
            ""
        };

        private static List<Student> BuildCrowd(Random random, IPasswordHasher<Student> passwordHasher)
        {
            var students = new List<Student>();
            var usedNames = new HashSet<string>();

            while (students.Count < 56)
            {
                var name = $"{Pick(random, FirstNames)} {Pick(random, LastNames)}";
                if (!usedNames.Add(name))
                {
                    continue;
                }

                var (major, _) = Majors[random.Next(Majors.Length)];
                var email = name.ToLowerInvariant().Replace(' ', '.') + "@example.edu";

                students.Add(NewStudent(
                    passwordHasher,
                    name,
                    email,
                    major,
                    random.Next(1, 6),
                    Pick(random, Bios),
                    (NoiseLevel)random.Next(0, 3),
                    (StudyPace)random.Next(0, 3),
                    (GroupSize)random.Next(0, 3),
                    isDemo: false));
            }

            return students;
        }

        private static Student NewStudent(
            IPasswordHasher<Student> passwordHasher,
            string name,
            string email,
            string major,
            int year,
            string bio,
            NoiseLevel noise,
            StudyPace pace,
            GroupSize group,
            bool isDemo)
        {
            var student = new Student
            {
                Name = name,
                Email = email.ToLowerInvariant(),
                Major = major,
                Year = year,
                Bio = bio,
                PreferredNoise = noise,
                Pace = pace,
                PreferredGroupSize = group,
                IsDemo = isDemo
            };

            // Seeded accounts are entered through the demo sign-in route, which does not
            // check a password. Hashing an unguessable value means no seeded account can
            // be reached by guessing a password, including the demo personas.
            student.PasswordHash = passwordHasher.HashPassword(student, Guid.NewGuid().ToString());

            return student;
        }

        // --- enrolment and availability --------------------------------------

        /// <summary>
        /// Weighted towards the student's own department so shared courses cluster by major.
        /// A purely random spread across 25 courses would leave almost nobody with an overlap,
        /// and the matching would look broken rather than selective.
        /// </summary>
        private static void AssignCourses(
            AppDbContext db,
            Student student,
            IReadOnlyDictionary<string, List<Course>> coursesByDepartment,
            List<Course> allCourses,
            Random random)
        {
            var chosen = new HashSet<int>();

            if (coursesByDepartment.TryGetValue(student.Major, out var home))
            {
                foreach (var course in home.OrderBy(_ => random.Next()).Take(random.Next(2, 4)))
                {
                    chosen.Add(course.CourseId);
                }
            }

            var electives = random.Next(1, 3);
            foreach (var course in allCourses.OrderBy(_ => random.Next()).Take(electives))
            {
                chosen.Add(course.CourseId);
            }

            foreach (var courseId in chosen)
            {
                db.Enrollments.Add(new Enrollment { StudentId = student.StudentId, CourseId = courseId });
            }
        }

        /// <summary>
        /// Evenings and weekday afternoons are weighted higher, so overlaps concentrate
        /// where students realistically meet instead of spreading thinly across 21 blocks.
        /// </summary>
        private static void AssignAvailability(AppDbContext db, Student student, Random random)
        {
            var slots = new HashSet<(DayOfWeek, TimeBlock)>();
            var target = random.Next(4, 11);
            var guard = 0;

            while (slots.Count < target && guard++ < 200)
            {
                var day = (DayOfWeek)random.Next(0, 7);
                var block = WeightedBlock(random);

                var isWeekend = day is DayOfWeek.Saturday or DayOfWeek.Sunday;
                if (isWeekend && random.NextDouble() < 0.6)
                {
                    continue;
                }

                slots.Add((day, block));
            }

            foreach (var (day, block) in slots)
            {
                db.AvailabilitySlots.Add(new AvailabilitySlot
                {
                    StudentId = student.StudentId,
                    Day = day,
                    Block = block
                });
            }
        }

        private static TimeBlock WeightedBlock(Random random)
        {
            var roll = random.NextDouble();
            if (roll < 0.20) return TimeBlock.Morning;
            if (roll < 0.55) return TimeBlock.Afternoon;
            return TimeBlock.Evening;
        }

        // --- existing activity -------------------------------------------------

        /// <summary>
        /// A few requests and accepted connections already in place, so the requests page
        /// is not empty the first time a demo persona opens it.
        /// </summary>
        private static async Task SeedRequestsAsync(AppDbContext db, List<Student> students, Random random)
        {
            var personas = students.Where(s => s.IsDemo).ToList();
            var others = students.Where(s => !s.IsDemo).ToList();
            var used = new HashSet<(int, int)>();

            foreach (var persona in personas)
            {
                // Two people waiting on the persona's answer.
                foreach (var sender in others.OrderBy(_ => random.Next()).Take(2))
                {
                    AddRequest(db, used, random, sender.StudentId, persona.StudentId, RequestStatus.Pending, null);
                }

                // One established study partner.
                var partner = others
                    .OrderBy(_ => random.Next())
                    .FirstOrDefault(o => !used.Contains((persona.StudentId, o.StudentId)) &&
                                         !used.Contains((o.StudentId, persona.StudentId)));

                if (partner != null)
                {
                    AddRequest(db, used, random, persona.StudentId, partner.StudentId, RequestStatus.Accepted,
                        DateTime.UtcNow.AddDays(-random.Next(2, 20)));
                }
            }

            await db.SaveChangesAsync();
        }

        private static void AddRequest(
            AppDbContext db,
            HashSet<(int, int)> used,
            Random random,
            int fromId,
            int toId,
            RequestStatus status,
            DateTime? respondedAt)
        {
            // Both orderings are rejected before anything is recorded: a pair must not
            // appear twice in either direction, and the schema forbids self-requests.
            if (fromId == toId ||
                used.Contains((fromId, toId)) ||
                used.Contains((toId, fromId)))
            {
                return;
            }

            used.Add((fromId, toId));

            db.StudyRequests.Add(new StudyRequest
            {
                FromStudentId = fromId,
                ToStudentId = toId,
                Status = status,
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30)),
                RespondedAt = respondedAt
            });
        }

        private static string Pick(Random random, string[] values) => values[random.Next(values.Length)];
    }
}
