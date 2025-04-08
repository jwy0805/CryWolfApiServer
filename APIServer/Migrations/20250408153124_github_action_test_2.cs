using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiServer.Migrations
{
    /// <inheritdoc />
    public partial class github_action_test_2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Act",
                table: "TempUser");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Act",
                table: "TempUser",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
