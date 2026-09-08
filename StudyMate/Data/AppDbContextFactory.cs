using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StudyMate.Data
{
    /// <summary>Keeps EF tooling from starting the web host while it creates migrations.</summary>
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<AppDbContext>();
            builder.UseSqlite(new SqliteConnectionStringBuilder { DataSource = "studymate-design.db" }.ConnectionString);
            return new AppDbContext(builder.Options);
        }
    }
}
