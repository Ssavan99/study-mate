using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudyMate.Data;
namespace StudyMate.Data.Migrations;
[DbContext(typeof(AppDbContext))]
[Migration("20260907010000_SafetyModeration")]
public partial class SafetyModeration : Migration
{
 protected override void Up(MigrationBuilder m)
 {
  m.AddColumn<bool>("IsSuspended", "Students", "INTEGER", nullable:false, defaultValue:false); m.AddColumn<bool>("IsAdmin", "Students", "INTEGER", nullable:false, defaultValue:false);
  m.CreateTable(name:"Blocks", columns: t => new { Id=t.Column<int>("INTEGER", nullable:false).Annotation("Sqlite:Autoincrement",true), BlockerStudentId=t.Column<int>("INTEGER",nullable:false), BlockedStudentId=t.Column<int>("INTEGER",nullable:false), CreatedAt=t.Column<DateTime>("TEXT",nullable:false)}, constraints: c=>c.PrimaryKey("PK_Blocks",x=>x.Id));
  m.CreateTable(name:"Reports", columns: t => new { Id=t.Column<int>("INTEGER",nullable:false).Annotation("Sqlite:Autoincrement",true), ReporterStudentId=t.Column<int>("INTEGER",nullable:false), ReportedStudentId=t.Column<int>("INTEGER",nullable:false), Reason=t.Column<int>("INTEGER",nullable:false), Detail=t.Column<string>("TEXT",maxLength:1000,nullable:true), CreatedAt=t.Column<DateTime>("TEXT",nullable:false), Status=t.Column<int>("INTEGER",nullable:false), ReviewedAt=t.Column<DateTime>("TEXT",nullable:true), ReviewerNote=t.Column<string>("TEXT",maxLength:1000,nullable:true)}, constraints: c=>c.PrimaryKey("PK_Reports",x=>x.Id));
  m.CreateIndex("IX_Blocks_BlockerStudentId_BlockedStudentId","Blocks",new[]{"BlockerStudentId","BlockedStudentId"},unique:true);
 }
 protected override void Down(MigrationBuilder m) { m.DropTable("Blocks");m.DropTable("Reports");m.Sql("ALTER TABLE Students DROP COLUMN IsSuspended; ALTER TABLE Students DROP COLUMN IsAdmin;"); }
}
