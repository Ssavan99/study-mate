using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyMate.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniversityCoursesAndSeekingPartner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Courses_Code",
                table: "Courses");

            migrationBuilder.AddColumn<bool>(
                name: "SeekingPartner",
                table: "Enrollments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "University",
                table: "Courses",
                type: "TEXT",
                maxLength: 120,
                nullable: false,
                defaultValue: "",
                collation: "NOCASE");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_University_Code",
                table: "Courses",
                columns: new[] { "University", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Courses_University_Code",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "SeekingPartner",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "University",
                table: "Courses");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_Code",
                table: "Courses",
                column: "Code",
                unique: true);
        }
    }
}
