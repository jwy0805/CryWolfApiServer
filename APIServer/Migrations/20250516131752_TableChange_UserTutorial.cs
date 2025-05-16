using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiServer.Migrations
{
    /// <inheritdoc />
    public partial class TableChange_UserTutorial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // (1) 기존 테이블 통째로 삭제
            migrationBuilder.DropTable(name: "UserTutorial");

            // (2) 새 구조로 테이블 생성
            migrationBuilder.CreateTable(
                name: "UserTutorial",
                columns: table => new
                {
                    UserTutorialId = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId         = table.Column<int>(nullable: false),
                    TutorialType   = table.Column<int>(nullable: false),
                    TutorialStep   = table.Column<int>(nullable: false),
                    Done           = table.Column<bool>(nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTutorial", x => x.UserTutorialId);
                    table.ForeignKey(
                        name: "FK_UserTutorial_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            // (3) 중복 방지용 유니크 인덱스
            migrationBuilder.CreateIndex(
                name: "IX_UserTutorial_UserId_TutorialType",
                table: "UserTutorial",
                columns: new[] { "UserId", "TutorialType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UserTutorial");
        }
    }
}
