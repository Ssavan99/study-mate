using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyMate.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoursesAndPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoContentType",
                table: "Students",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "PhotoData",
                table: "Students",
                type: "BLOB",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoContentType",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "PhotoData",
                table: "Students");
        }
    }
}
