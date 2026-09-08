using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using StudyMate.Data;

#nullable disable

namespace StudyMate.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260907000000_UniversityIdentityAndCatalog")]
    public partial class UniversityIdentityAndCatalog : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Universities",
                columns: table => new
                {
                    UniversityId = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 140, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_Universities", x => x.UniversityId));

            migrationBuilder.CreateTable(
                name: "UniversityEmailDomains",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    UniversityId = table.Column<int>(type: "INTEGER", nullable: false),
                    Domain = table.Column<string>(type: "TEXT", maxLength: 253, nullable: false, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UniversityEmailDomains", x => x.Id);
                    table.ForeignKey("FK_UniversityEmailDomains_Universities_UniversityId", x => x.UniversityId, "Universities", "UniversityId", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(name: "IX_Universities_Slug", table: "Universities", column: "Slug", unique: true);
            migrationBuilder.CreateIndex(name: "IX_UniversityEmailDomains_Domain", table: "UniversityEmailDomains", column: "Domain", unique: true);
            migrationBuilder.CreateIndex(name: "IX_UniversityEmailDomains_UniversityId", table: "UniversityEmailDomains", column: "UniversityId");

            migrationBuilder.AddColumn<int>(name: "UniversityId", table: "Students", type: "INTEGER", nullable: true);
            migrationBuilder.AddColumn<string>(name: "VerifiedEmail", table: "Students", type: "TEXT", maxLength: 160, nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "EmailVerifiedAt", table: "Students", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<int>(name: "UniversityId", table: "Courses", type: "INTEGER", nullable: true);

            // SQLite has no regular-expression replace. Repeated whitespace collapsing
            // handles the legacy free-text values without a manual deployment step.
            migrationBuilder.Sql(@"
CREATE TEMP TABLE _UniversityBackfill AS
SELECT NormalizedName, MIN(DisplayName) AS DisplayName FROM (
  SELECT lower(trim(replace(replace(replace(replace(replace(replace(University, char(9), ' '), '  ', ' '), '  ', ' '), '  ', ' '), '  ', ' '), '  ', ' '))) AS NormalizedName,
         trim(University) AS DisplayName FROM Students
  UNION ALL
  SELECT lower(trim(replace(replace(replace(replace(replace(replace(University, char(9), ' '), '  ', ' '), '  ', ' '), '  ', ' '), '  ', ' '), '  ', ' '))) AS NormalizedName,
         trim(University) AS DisplayName FROM Courses
) WHERE NormalizedName <> '' GROUP BY NormalizedName;
INSERT INTO Universities (Name, Slug, IsActive)
SELECT DisplayName, 'legacy-' || lower(hex(NormalizedName)), 1 FROM _UniversityBackfill;
UPDATE Students SET UniversityId = (
  SELECT u.UniversityId FROM Universities u
  JOIN _UniversityBackfill b ON u.Slug = 'legacy-' || lower(hex(b.NormalizedName))
  WHERE b.NormalizedName = lower(trim(replace(replace(replace(replace(replace(replace(Students.University, char(9), ' '), '  ', ' '), '  ', ' '), '  ', ' '), '  ', ' '), '  ', ' ')))
);
UPDATE Courses SET UniversityId = (
  SELECT u.UniversityId FROM Universities u
  JOIN _UniversityBackfill b ON u.Slug = 'legacy-' || lower(hex(b.NormalizedName))
  WHERE b.NormalizedName = lower(trim(replace(replace(replace(replace(replace(replace(Courses.University, char(9), ' '), '  ', ' '), '  ', ' '), '  ', ' '), '  ', ' '), '  ', ' ')))
);
DROP TABLE _UniversityBackfill;
");

            // Existing databases may contain blank legacy strings. Preserve the rows by
            // assigning them a durable, inactive legacy entity rather than dropping data.
            migrationBuilder.Sql(@"
INSERT INTO Universities (Name, Slug, IsActive)
SELECT 'Unspecified university', 'legacy-unspecified', 0
WHERE NOT EXISTS (SELECT 1 FROM Universities WHERE Slug = 'legacy-unspecified')
  AND (EXISTS (SELECT 1 FROM Students WHERE UniversityId IS NULL)
       OR EXISTS (SELECT 1 FROM Courses WHERE UniversityId IS NULL));
UPDATE Students SET UniversityId = (SELECT UniversityId FROM Universities WHERE Slug = 'legacy-unspecified') WHERE UniversityId IS NULL;
UPDATE Courses SET UniversityId = (SELECT UniversityId FROM Universities WHERE Slug = 'legacy-unspecified') WHERE UniversityId IS NULL;
");

            migrationBuilder.DropIndex(name: "IX_Courses_University_Code", table: "Courses");
            migrationBuilder.Sql("ALTER TABLE Students DROP COLUMN University;");
            migrationBuilder.Sql("ALTER TABLE Courses DROP COLUMN University;");

            migrationBuilder.CreateIndex(name: "IX_Students_UniversityId", table: "Students", column: "UniversityId");
            // Do not make this unique during the backfill: old free-text variants can
            // collapse to one university and still have distinct legacy course rows.
            migrationBuilder.CreateIndex(name: "IX_Courses_UniversityId_Code", table: "Courses", columns: new[] { "UniversityId", "Code" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Students_UniversityId", table: "Students");
            migrationBuilder.DropIndex(name: "IX_Courses_UniversityId_Code", table: "Courses");
            migrationBuilder.AddColumn<string>(name: "University", table: "Students", type: "TEXT", maxLength: 120, nullable: false, defaultValue: "", collation: "NOCASE");
            migrationBuilder.AddColumn<string>(name: "University", table: "Courses", type: "TEXT", maxLength: 120, nullable: false, defaultValue: "", collation: "NOCASE");
            migrationBuilder.Sql("UPDATE Students SET University = COALESCE((SELECT Name FROM Universities WHERE Universities.UniversityId = Students.UniversityId), ''); UPDATE Courses SET University = COALESCE((SELECT Name FROM Universities WHERE Universities.UniversityId = Courses.UniversityId), '');");
            migrationBuilder.Sql("ALTER TABLE Students DROP COLUMN UniversityId; ALTER TABLE Students DROP COLUMN VerifiedEmail; ALTER TABLE Students DROP COLUMN EmailVerifiedAt; ALTER TABLE Courses DROP COLUMN UniversityId;");
            migrationBuilder.CreateIndex(name: "IX_Courses_University_Code", table: "Courses", columns: new[] { "University", "Code" }, unique: true);
            migrationBuilder.DropTable(name: "UniversityEmailDomains");
            migrationBuilder.DropTable(name: "Universities");
        }
    }
}
