using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiServer.Migrations
{
    /// <inheritdoc />
    public partial class REMOVE_KEY_TRANSACTION : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 0건 전제: 깔끔하게 Drop & Create가 가장 안전
            migrationBuilder.Sql(@"SET FOREIGN_KEY_CHECKS=0;");
            migrationBuilder.DropTable(name: "Transaction");
            migrationBuilder.Sql(@"SET FOREIGN_KEY_CHECKS=1;");

            migrationBuilder.CreateTable(
                name: "Transaction",
                columns: table => new
                {
                    TransactionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),

                    UserId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    PurchaseAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),

                    Currency = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CashCurrency = table.Column<int>(type: "int", nullable: false),

                    StoreTransactionId = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false, defaultValue: "")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StoreType = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transaction", x => x.TransactionId);
                    table.ForeignKey(
                        name: "FK_Transaction_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_UserId",
                table: "Transaction",
                column: "UserId");

            // 중복 결제 방지(0건이니 바로 Unique로)
            migrationBuilder.CreateIndex(
                name: "UX_Transaction_StoreType_StoreTransactionId",
                table: "Transaction",
                columns: new[] { "StoreType", "StoreTransactionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"SET FOREIGN_KEY_CHECKS=0;");
            migrationBuilder.DropTable(name: "Transaction");
            migrationBuilder.Sql(@"SET FOREIGN_KEY_CHECKS=1;");

            migrationBuilder.CreateTable(
                name: "Transaction",
                columns: table => new
                {
                    TransactionTimestamp = table.Column<long>(type: "bigint(20)", nullable: false),
                    UserId = table.Column<int>(type: "int(11)", nullable: false),
                    ProductId = table.Column<int>(type: "int(11)", nullable: false),
                    Count = table.Column<int>(type: "int(11)", nullable: false),
                    PurchaseAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Currency = table.Column<int>(type: "int(11)", nullable: false),
                    Status = table.Column<int>(type: "int(11)", nullable: false),
                    CashCurrency = table.Column<int>(type: "int(11)", nullable: false),
                    ReceiptRaw = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false, defaultValue: "")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StoreTransactionId = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false, defaultValue: "")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StoreType = table.Column<int>(type: "int(11)", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transaction", x => new { x.TransactionTimestamp, x.UserId });
                    table.ForeignKey(
                        name: "FK_Transaction_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_UserId",
                table: "Transaction",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_StoreType_StoreTransactionId",
                table: "Transaction",
                columns: new[] { "StoreType", "StoreTransactionId" });
        }
    }
}
