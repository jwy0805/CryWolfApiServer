using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountServer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DrawFriendlyMatch",
                table: "UserMatch");

            migrationBuilder.DropColumn(
                name: "DrawRankMatch",
                table: "UserMatch");

            migrationBuilder.AddColumn<int>(
                name: "HighestRankPoint",
                table: "UserStats",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HighestRankPoint",
                table: "UserStats");

            migrationBuilder.AddColumn<int>(
                name: "DrawFriendlyMatch",
                table: "UserMatch",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DrawRankMatch",
                table: "UserMatch",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
