using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiServer.Migrations
{
    /// <inheritdoc />
    public partial class github_actions_test_14 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TestValue",
                table: "TempUser");

            migrationBuilder.RenameColumn(
                name: "TestValue2",
                table: "TempUser",
                newName: "TestValue13");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TestValue13",
                table: "TempUser",
                newName: "TestValue2");

            migrationBuilder.AddColumn<int>(
                name: "TestValue",
                table: "TempUser",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
