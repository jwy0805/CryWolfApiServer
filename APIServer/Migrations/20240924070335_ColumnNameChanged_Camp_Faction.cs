using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountServer.Migrations
{
    /// <inheritdoc />
    public partial class ColumnNameChanged_Camp_Faction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Camp",
                table: "Unit",
                newName: "Faction");

            migrationBuilder.RenameColumn(
                name: "Camp",
                table: "Deck",
                newName: "Faction");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Faction",
                table: "Unit",
                newName: "Camp");

            migrationBuilder.RenameColumn(
                name: "Faction",
                table: "Deck",
                newName: "Camp");
        }
    }
}
