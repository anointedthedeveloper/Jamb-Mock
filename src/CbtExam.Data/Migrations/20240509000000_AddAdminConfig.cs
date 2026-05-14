using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CbtExam.Data.Migrations;

public partial class AddAdminConfig : Migration
{
    protected override void Up(MigrationBuilder mb)
    {
        mb.CreateTable("AdminConfigs", t => new
        {
            Id = t.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
            AccessCode = t.Column<string>(maxLength: 50, nullable: false),
            Username = t.Column<string>(maxLength: 100, nullable: false),
            IsActive = t.Column<bool>(nullable: false, defaultValue: true),
            CreatedAt = t.Column<DateTime>(nullable: false),
            LastLoginAt = t.Column<DateTime>(nullable: true),
            Notes = t.Column<string>(maxLength: 500, nullable: true)
        }, constraints: t => t.PrimaryKey("PK_AdminConfigs", x => x.Id));
    }

    protected override void Down(MigrationBuilder mb)
    {
        mb.DropTable("AdminConfigs");
    }
}
