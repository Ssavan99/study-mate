using Microsoft.EntityFrameworkCore;
using StudyMate.Models;

namespace StudyMate.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Student> Students { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<AvailabilitySlot> AvailabilitySlots { get; set; }
        public DbSet<StudyRequest> StudyRequests { get; set; }
        public DbSet<Pass> Passes { get; set; }
        public DbSet<University> Universities { get; set; }
        public DbSet<UniversityEmailDomain> UniversityEmailDomains { get; set; }
        public DbSet<Block> Blocks { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<StudySession> StudySessions { get; set; }
        public DbSet<SessionParticipant> SessionParticipants { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // SQLite compares TEXT with BINARY collation by default, which would let
            // alice@x.edu and Alice@x.edu register as two separate accounts. Emails are
            // also lowercased on write; this makes the uniqueness guarantee unconditional.
            modelBuilder.Entity<Student>()
                .Property(s => s.Email)
                .UseCollation("NOCASE");

            modelBuilder.Entity<Student>()
                .HasIndex(s => s.Email)
                .IsUnique();

            modelBuilder.Entity<Course>()
                .HasIndex(c => new { c.UniversityId, c.Code });

            modelBuilder.Entity<University>()
                .HasIndex(u => u.Slug)
                .IsUnique();

            modelBuilder.Entity<UniversityEmailDomain>()
                .Property(d => d.Domain)
                .UseCollation("NOCASE");

            modelBuilder.Entity<UniversityEmailDomain>()
                .HasIndex(d => d.Domain)
                .IsUnique();

            modelBuilder.Entity<Student>()
                .HasOne(s => s.University)
                .WithMany(u => u.Students)
                .HasForeignKey(s => s.UniversityId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Course>()
                .HasOne(c => c.University)
                .WithMany(u => u.Courses)
                .HasForeignKey(c => c.UniversityId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Enrollment>()
                .HasKey(e => new { e.StudentId, e.CourseId });

            modelBuilder.Entity<AvailabilitySlot>()
                .HasKey(a => new { a.StudentId, a.Day, a.Block });

            modelBuilder.Entity<Pass>()
                .HasKey(p => new { p.StudentId, p.PassedStudentId });

            // The passed student needs its own foreign key, otherwise passes outlive the
            // student they refer to and nothing stops a pass pointing at an unknown id.
            modelBuilder.Entity<Pass>()
                .HasOne<Student>()
                .WithMany()
                .HasForeignKey(p => p.PassedStudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Pass>()
                .ToTable(t => t.HasCheckConstraint("CK_Pass_NotSelf", "StudentId <> PassedStudentId"));

            modelBuilder.Entity<Block>().HasIndex(b => new { b.BlockerStudentId, b.BlockedStudentId }).IsUnique();
            modelBuilder.Entity<Block>().ToTable(t => t.HasCheckConstraint("CK_Block_NotSelf", "BlockerStudentId <> BlockedStudentId"));
            modelBuilder.Entity<Report>().ToTable(t => t.HasCheckConstraint("CK_Report_NotSelf", "ReporterStudentId <> ReportedStudentId"));
            modelBuilder.Entity<SessionParticipant>().HasKey(p => new { p.SessionId, p.StudentId });

            // Two navigation paths from StudyRequest to Student, so both relationships are
            // configured explicitly. Both cascade: SQLite has no restriction on multiple
            // cascade paths, and leaving the incoming side as Restrict would make a student
            // who had ever been asked to study impossible to delete.
            modelBuilder.Entity<StudyRequest>()
                .HasOne(r => r.FromStudent)
                .WithMany()
                .HasForeignKey(r => r.FromStudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudyRequest>()
                .HasOne(r => r.ToStudent)
                .WithMany()
                .HasForeignKey(r => r.ToStudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudyRequest>()
                .ToTable(t => t.HasCheckConstraint("CK_StudyRequest_NotSelf", "FromStudentId <> ToStudentId"));

            // One request per ordered pair, for any status. A decline is final: the deck
            // also excludes declined pairs permanently, so the two rules agree.
            modelBuilder.Entity<StudyRequest>()
                .HasIndex(r => new { r.FromStudentId, r.ToStudentId })
                .IsUnique();
        }
    }
}
