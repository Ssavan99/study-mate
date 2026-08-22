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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Student>()
                .HasIndex(s => s.Email)
                .IsUnique();

            modelBuilder.Entity<Course>()
                .HasIndex(c => c.Code)
                .IsUnique();

            modelBuilder.Entity<Enrollment>()
                .HasKey(e => new { e.StudentId, e.CourseId });

            modelBuilder.Entity<AvailabilitySlot>()
                .HasKey(a => new { a.StudentId, a.Day, a.Block });

            modelBuilder.Entity<Pass>()
                .HasKey(p => new { p.StudentId, p.PassedStudentId });

            // Two navigation paths from StudyRequest to Student, so both relationships
            // are configured explicitly. Cascade delete is disabled on the incoming side
            // because SQLite cannot resolve the multiple cascade paths this would create.
            modelBuilder.Entity<StudyRequest>()
                .HasOne(r => r.FromStudent)
                .WithMany()
                .HasForeignKey(r => r.FromStudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudyRequest>()
                .HasOne(r => r.ToStudent)
                .WithMany()
                .HasForeignKey(r => r.ToStudentId)
                .OnDelete(DeleteBehavior.Restrict);

            // A student may only have one outstanding request toward another student.
            modelBuilder.Entity<StudyRequest>()
                .HasIndex(r => new { r.FromStudentId, r.ToStudentId })
                .IsUnique();
        }
    }
}
