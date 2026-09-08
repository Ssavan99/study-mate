using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudyMate.Models;
using StudyMate.Services;

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
            var universities = await SeedUniversitiesAndCatalogAsync(db);

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

            var courses = await db.Courses.ToListAsync();
            var coursesByUniversity = courses
                .GroupBy(c => c.UniversityId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var students = new List<Student>();
            var personas = BuildPersonas(passwordHasher, universities[PrimaryUniversity].UniversityId);
            var usedNames = new HashSet<string>(personas.Select(s => s.Name));
            students.AddRange(personas);
            // 50 at the personas' university (so the demo decks stay full), 6 at a
            // second university that should never appear in anyone's matches there.
            students.AddRange(BuildCrowd(random, passwordHasher, 50, universities[PrimaryUniversity].UniversityId, usedNames));
            students.AddRange(BuildCrowd(random, passwordHasher, 6, universities[SecondaryUniversity].UniversityId, usedNames));
            students.First(s => s.IsDemo).IsAdmin = true;

            db.Students.AddRange(students);
            await db.SaveChangesAsync();

            foreach (var student in students)
            {
                AssignCourses(db, student, coursesByUniversity[student.UniversityId!.Value], random);
                AssignAvailability(db, student, random);
            }

            await db.SaveChangesAsync();
            await SeedRequestsAsync(db, students, random);

            await transaction.CommitAsync();
        }

        // --- courses --------------------------------------------------------

        /// <summary>
        /// Two catalogs with deliberately different department abbreviations and number
        /// ranges — the same subject is "CSCE 155" at one school and "CS 227" at the other.
        /// That difference is the whole reason courses are scoped per university rather
        /// than pooled globally.
        /// </summary>
        private static async Task<Dictionary<string, University>> SeedUniversitiesAndCatalogAsync(AppDbContext db)
        {
            var definitions = new[]
            {
                new { Name = PrimaryUniversity, Slug = "university-of-nebraska-lincoln", Domains = new[] { "unl.edu" } },
                new { Name = SecondaryUniversity, Slug = "iowa-state-university", Domains = new[] { "iastate.edu" } }
            };
            var result = new Dictionary<string, University>();
            foreach (var definition in definitions)
            {
                var university = await db.Universities.Include(u => u.EmailDomains)
                    .SingleOrDefaultAsync(u => u.Slug == definition.Slug);
                if (university == null)
                {
                    university = new University { Name = definition.Name, Slug = definition.Slug, IsActive = true };
                    db.Universities.Add(university);
                }
                foreach (var domain in definition.Domains.Where(domain => university.EmailDomains.All(d => d.Domain != domain)))
                    university.EmailDomains.Add(new UniversityEmailDomain { Domain = domain });
                result.Add(definition.Name, university);
            }
            await db.SaveChangesAsync();

            var catalogDirectory = Path.Combine(AppContext.BaseDirectory, "Data", "Catalog");
            foreach (var university in result.Values)
            {
                var path = Path.Combine(catalogDirectory, $"{university.Slug}.json");
                if (!File.Exists(path)) continue;
                var existingCourses = await db.Courses.Where(c => c.UniversityId == university.UniversityId).ToListAsync();
                foreach (var course in CatalogImportPlanner.MissingCourses(CatalogLoader.Load(path), existingCourses))
                {
                    db.Courses.Add(new Course { UniversityId = university.UniversityId, Code = course.Code, Title = course.Title, Department = course.Department });
                }
            }
            await db.SaveChangesAsync();
            return result;
        }

        // --- the four demo personas ------------------------------------------

        /// <summary>
        /// Hand-written so the landing page has recognisable, distinct entry points.
        /// Their majors are the most common ones in the crowd, which guarantees each
        /// of them opens onto a populated deck rather than an empty one.
        /// </summary>
        /// <summary>All four demo personas share this university, so their decks are never empty.</summary>
        public const string PrimaryUniversity = "University of Nebraska–Lincoln";

        /// <summary>
        /// A second, smaller university seeded specifically so the same-university match
        /// filter has something to visibly exclude — with only one university in the data,
        /// the filter would be unverifiable in the demo even if it worked correctly.
        /// </summary>
        public const string SecondaryUniversity = "Iowa State University";

        private static List<Student> BuildPersonas(IPasswordHasher<Student> passwordHasher, int universityId) => new()
        {
            NewStudent(passwordHasher, "Maya Chen", "maya.chen@example.edu", "Computer Science", 3,
                "Third year CS, deep in algorithms. I like working through problem sets out loud with someone.",
                NoiseLevel.Discussion, StudyPace.Steady, GroupSize.OneOnOne, universityId, isDemo: true),

            NewStudent(passwordHasher, "Daniel Okafor", "daniel.okafor@example.edu", "Mathematics", 2,
                "Maths major who mostly needs someone to sit with in the library and stay off my phone.",
                NoiseLevel.Silent, StudyPace.Steady, GroupSize.OneOnOne, universityId, isDemo: true),

            NewStudent(passwordHasher, "Priya Raman", "priya.raman@example.edu", "Computer Science", 4,
                "Final year, juggling a capstone. Realistically I study in bursts before deadlines.",
                NoiseLevel.Quiet, StudyPace.Crammer, GroupSize.SmallGroup, universityId, isDemo: true),

            NewStudent(passwordHasher, "Sofia Duarte", "sofia.duarte@example.edu", "Biology", 2,
                "Pre-med, so a lot of memorisation. Happy to quiz people if they quiz me back.",
                NoiseLevel.Quiet, StudyPace.Mixed, GroupSize.Either, universityId, isDemo: true)
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

        private static List<Student> BuildCrowd(
            Random random,
            IPasswordHasher<Student> passwordHasher,
            int count,
            int universityId,
            HashSet<string> usedNames)
        {
            var students = new List<Student>();

            while (students.Count < count)
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
                    universityId,
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
            int universityId,
            bool isDemo)
        {
            var student = new Student
            {
                Name = name,
                Email = email.ToLowerInvariant(),
                Major = major,
                UniversityId = universityId,
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
        /// <summary>Major to the department abbreviations that teach it, per catalog.</summary>
        private static readonly Dictionary<string, string[]> MajorDepartments = new()
        {
            ["Computer Science"] = new[] { "CSCE", "CS" },
            ["Mathematics"] = new[] { "MATH", "STAT" },
            ["Physics"] = new[] { "PHYS" },
            ["Biology"] = new[] { "BIOL" },
            ["Chemistry"] = new[] { "CHEM" },
            ["Economics"] = new[] { "ECON" },
            ["Psychology"] = new[] { "PSYC", "PSYCH" }
        };

        private static void AssignCourses(
            AppDbContext db,
            Student student,
            List<Course> universityCourses,
            Random random)
        {
            var chosen = new HashSet<int>();

            if (MajorDepartments.TryGetValue(student.Major, out var departments))
            {
                var home = universityCourses.Where(c => departments.Contains(c.Department)).ToList();
                foreach (var course in home.OrderBy(_ => random.Next()).Take(random.Next(2, 4)))
                {
                    chosen.Add(course.CourseId);
                }
            }

            var electives = random.Next(1, 3);
            foreach (var course in universityCourses.OrderBy(_ => random.Next()).Take(electives))
            {
                chosen.Add(course.CourseId);
            }

            foreach (var courseId in chosen)
            {
                // Most enrolments are open to a partner, but not all — a schedule where
                // every single course is flagged would make the toggle look decorative
                // instead of demonstrating that it actually gates matching.
                db.Enrollments.Add(new Enrollment
                {
                    StudentId = student.StudentId,
                    CourseId = courseId,
                    SeekingPartner = random.NextDouble() < 0.78
                });
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
            var used = new HashSet<(int, int)>();

            foreach (var persona in personas)
            {
                // Same university only — a real request could never exist otherwise,
                // since the deck it would have come from is already filtered that way.
                var others = students
                    .Where(s => !s.IsDemo && s.UniversityId == persona.UniversityId)
                    .ToList();

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
