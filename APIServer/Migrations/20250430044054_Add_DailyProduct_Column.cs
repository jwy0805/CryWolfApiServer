using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiServer.Migrations
{
    /// <inheritdoc />
    public partial class Add_DailyProduct_Column : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Probability",
                table: "DailyProduct",
                newName: "Weight");

            migrationBuilder.AddColumn<int>(
                name: "Class",
                table: "DailyProduct",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Class",
                table: "DailyProduct");

            migrationBuilder.RenameColumn(
                name: "Weight",
                table: "DailyProduct",
                newName: "Probability");
        }
    }
}
