using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiServer.Migrations
{
    /// <inheritdoc />
    public partial class ADD_RefreshToken_ClientType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) 컬럼 추가
            migrationBuilder.AddColumn<int>(
                name: "ClientType",
                table: "RefreshToken",
                type: "int",
                nullable: false,
                defaultValue: 2); // Mobile=2 같은 값으로 맞추는 걸 권장 (아래 설명 참고)

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAt",
                table: "RefreshToken",
                type: "datetime(6)",
                nullable: true);

            // 2) 인덱스 생성(먼저)
            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_Token",
                table: "RefreshToken",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_UserId_ClientType",
                table: "RefreshToken",
                columns: new[] { "UserId", "ClientType" },
                unique: true);

            // 3) 이제 기존 UserId 단일 인덱스를 드랍해도 FK가 유지됨(복합 인덱스가 대체)
            migrationBuilder.DropIndex(
                name: "IX_RefreshToken_UserId",
                table: "RefreshToken");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop 순서는 반대로
            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_UserId",
                table: "RefreshToken",
                column: "UserId");

            migrationBuilder.DropIndex(
                name: "IX_RefreshToken_Token",
                table: "RefreshToken");

            migrationBuilder.DropIndex(
                name: "IX_RefreshToken_UserId_ClientType",
                table: "RefreshToken");

            migrationBuilder.DropColumn(
                name: "ClientType",
                table: "RefreshToken");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "RefreshToken");
        }
    }
}
