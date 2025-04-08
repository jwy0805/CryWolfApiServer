using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiServer.Migrations
{
    /// <inheritdoc />
    public partial class github_action_test_6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TempValue",
                table: "TempUser");

            migrationBuilder.RenameColumn(
                name: "TempValue2",
                table: "TempUser",
                newName: "TestValue");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TestValue",
                table: "TempUser",
                newName: "TempValue2");

            migrationBuilder.AddColumn<int>(
                name: "TempValue",
                table: "TempUser",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
