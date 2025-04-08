using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountServer.Migrations
{
    /// <inheritdoc />
    public partial class stage_column_added : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssetId",
                table: "Stage",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CharacterId",
                table: "Stage",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MapId",
                table: "Stage",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssetId",
                table: "Stage");

            migrationBuilder.DropColumn(
                name: "CharacterId",
                table: "Stage");

            migrationBuilder.DropColumn(
                name: "MapId",
                table: "Stage");
        }
    }
}
