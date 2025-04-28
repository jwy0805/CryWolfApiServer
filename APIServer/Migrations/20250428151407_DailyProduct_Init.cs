using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiServer.Migrations
{
    /// <inheritdoc />
    public partial class DailyProduct_Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailyProduct_Product_DailyProductId",
                table: "DailyProduct");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DailyProduct",
                table: "DailyProduct");

            migrationBuilder.DropColumn(
                name: "DailyProductId",
                table: "DailyProduct");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DailyProduct",
                table: "DailyProduct",
                column: "ProductId");

            migrationBuilder.CreateTable(
                name: "UserDailyProduct",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Slot = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    SeedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RefreshIndex = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    RefreshAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDailyProduct", x => new { x.UserId, x.Slot });
                    table.ForeignKey(
                        name: "FK_UserDailyProduct_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserDailyProduct_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_UserDailyProduct_ProductId",
                table: "UserDailyProduct",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_DailyProduct_Product_ProductId",
                table: "DailyProduct",
                column: "ProductId",
                principalTable: "Product",
                principalColumn: "ProductId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailyProduct_Product_ProductId",
                table: "DailyProduct");

            migrationBuilder.DropTable(
                name: "UserDailyProduct");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DailyProduct",
                table: "DailyProduct");

            migrationBuilder.AddColumn<int>(
                name: "DailyProductId",
                table: "DailyProduct",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_DailyProduct",
                table: "DailyProduct",
                column: "DailyProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_DailyProduct_Product_DailyProductId",
                table: "DailyProduct",
                column: "DailyProductId",
                principalTable: "Product",
                principalColumn: "ProductId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
