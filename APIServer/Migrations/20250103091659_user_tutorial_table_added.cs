using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountServer.Migrations
{
    /// <inheritdoc />
    public partial class user_tutorial_table_added : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserTutorial",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TutorialType = table.Column<int>(type: "int", nullable: false),
                    TutorialStep = table.Column<int>(type: "int", nullable: false),
                    Done = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTutorial", x => new { x.UserId, x.TutorialType });
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserTutorial");
        }
    }
}
