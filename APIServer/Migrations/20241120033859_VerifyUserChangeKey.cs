using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountServer.Migrations
{
    /// <inheritdoc />
    public partial class VerifyUserChangeKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TempUser",
                table: "TempUser");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TempUser",
                table: "TempUser",
                columns: new[] { "TempUserAccount", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TempUser",
                table: "TempUser");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TempUser",
                table: "TempUser",
                column: "TempUserAccount");
        }
    }
}
