using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountServer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTableCol0326 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeckNumber",
                table: "Deck_Unit");

            migrationBuilder.AddColumn<int>(
                name: "DeckNumber",
                table: "Deck",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "LastPicked",
                table: "Deck",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeckNumber",
                table: "Deck");

            migrationBuilder.DropColumn(
                name: "LastPicked",
                table: "Deck");

            migrationBuilder.AddColumn<int>(
                name: "DeckNumber",
                table: "Deck_Unit",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
