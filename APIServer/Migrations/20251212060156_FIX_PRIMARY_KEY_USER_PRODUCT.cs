using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiServer.Migrations
{
    /// <inheritdoc />
    public partial class FIX_PRIMARY_KEY_USER_PRODUCT : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE `User_Product`
  DROP PRIMARY KEY,
  ADD PRIMARY KEY (`UserId`, `ProductId`, `AcquisitionPath`);
");

            migrationBuilder.AddColumn<string>(
                name: "ReceiptRaw",
                table: "Transaction",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "StoreTransactionId",
                table: "Transaction",
                type: "varchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "StoreType",
                table: "Transaction",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_StoreType_StoreTransactionId",
                table: "Transaction",
                columns: new[] { "StoreType", "StoreTransactionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transaction_StoreType_StoreTransactionId",
                table: "Transaction");

            migrationBuilder.DropColumn(
                name: "ReceiptRaw",
                table: "Transaction");

            migrationBuilder.DropColumn(
                name: "StoreTransactionId",
                table: "Transaction");

            migrationBuilder.DropColumn(
                name: "StoreType",
                table: "Transaction");

            migrationBuilder.Sql(@"
ALTER TABLE `User_Product`
  DROP PRIMARY KEY,
  ADD PRIMARY KEY (`UserId`, `ProductId`);
");
        }
    }
}
