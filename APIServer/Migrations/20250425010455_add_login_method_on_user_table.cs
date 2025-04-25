using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiServer.Migrations
{
    /// <inheritdoc />
    public partial class add_login_method_on_user_table : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LoginMethod",
                table: "User",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "TempPassword",
                table: "TempUser",
                type: "varchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "TempUserAccount",
                table: "TempUser",
                type: "varchar(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_UserId",
                table: "Transaction",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_UserId",
                table: "RefreshToken",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Mail_UserId",
                table: "Mail",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Friends_FriendId",
                table: "Friends",
                column: "FriendId");

            migrationBuilder.CreateIndex(
                name: "IX_Deck_UserId",
                table: "Deck",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_BattleSetting_User_UserId",
                table: "BattleSetting",
                column: "UserId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Deck_User_UserId",
                table: "Deck",
                column: "UserId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Deck_Unit_Deck_DeckId",
                table: "Deck_Unit",
                column: "DeckId",
                principalTable: "Deck",
                principalColumn: "DeckId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Friends_User_FriendId",
                table: "Friends",
                column: "FriendId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Friends_User_UserId",
                table: "Friends",
                column: "UserId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Mail_User_UserId",
                table: "Mail",
                column: "UserId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshToken_User_UserId",
                table: "RefreshToken",
                column: "UserId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Transaction_User_UserId",
                table: "Transaction",
                column: "UserId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_User_Character_User_UserId",
                table: "User_Character",
                column: "UserId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_User_Enchant_User_UserId",
                table: "User_Enchant",
                column: "UserId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_User_Material_User_UserId",
                table: "User_Material",
                column: "UserId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_User_Product_User_UserId",
                table: "User_Product",
                column: "UserId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_User_Sheep_User_UserId",
                table: "User_Sheep",
                column: "UserId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_User_Stage_User_UserId",
                table: "User_Stage",
                column: "UserId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_User_Unit_User_UserId",
                table: "User_Unit",
                column: "UserId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserStats_User_UserId",
                table: "UserStats",
                column: "UserId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserTutorial_User_UserId",
                table: "UserTutorial",
                column: "UserId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BattleSetting_User_UserId",
                table: "BattleSetting");

            migrationBuilder.DropForeignKey(
                name: "FK_Deck_User_UserId",
                table: "Deck");

            migrationBuilder.DropForeignKey(
                name: "FK_Deck_Unit_Deck_DeckId",
                table: "Deck_Unit");

            migrationBuilder.DropForeignKey(
                name: "FK_Friends_User_FriendId",
                table: "Friends");

            migrationBuilder.DropForeignKey(
                name: "FK_Friends_User_UserId",
                table: "Friends");

            migrationBuilder.DropForeignKey(
                name: "FK_Mail_User_UserId",
                table: "Mail");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshToken_User_UserId",
                table: "RefreshToken");

            migrationBuilder.DropForeignKey(
                name: "FK_Transaction_User_UserId",
                table: "Transaction");

            migrationBuilder.DropForeignKey(
                name: "FK_User_Character_User_UserId",
                table: "User_Character");

            migrationBuilder.DropForeignKey(
                name: "FK_User_Enchant_User_UserId",
                table: "User_Enchant");

            migrationBuilder.DropForeignKey(
                name: "FK_User_Material_User_UserId",
                table: "User_Material");

            migrationBuilder.DropForeignKey(
                name: "FK_User_Product_User_UserId",
                table: "User_Product");

            migrationBuilder.DropForeignKey(
                name: "FK_User_Sheep_User_UserId",
                table: "User_Sheep");

            migrationBuilder.DropForeignKey(
                name: "FK_User_Stage_User_UserId",
                table: "User_Stage");

            migrationBuilder.DropForeignKey(
                name: "FK_User_Unit_User_UserId",
                table: "User_Unit");

            migrationBuilder.DropForeignKey(
                name: "FK_UserStats_User_UserId",
                table: "UserStats");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTutorial_User_UserId",
                table: "UserTutorial");

            migrationBuilder.DropIndex(
                name: "IX_Transaction_UserId",
                table: "Transaction");

            migrationBuilder.DropIndex(
                name: "IX_RefreshToken_UserId",
                table: "RefreshToken");

            migrationBuilder.DropIndex(
                name: "IX_Mail_UserId",
                table: "Mail");

            migrationBuilder.DropIndex(
                name: "IX_Friends_FriendId",
                table: "Friends");

            migrationBuilder.DropIndex(
                name: "IX_Deck_UserId",
                table: "Deck");

            migrationBuilder.DropColumn(
                name: "LoginMethod",
                table: "User");

            migrationBuilder.AlterColumn<string>(
                name: "TempPassword",
                table: "TempUser",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(120)",
                oldMaxLength: 120)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "TempUserAccount",
                table: "TempUser",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(60)",
                oldMaxLength: 60)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
