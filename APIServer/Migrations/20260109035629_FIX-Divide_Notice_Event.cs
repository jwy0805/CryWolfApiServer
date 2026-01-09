using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiServer.Migrations
{
    public partial class FIXDivide_Notice_Event : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 목적: 로컬에서 "공지/이벤트 관련 테이블만" 전부 버리고, 최종 스키마로 100% 재생성
            // 주의: User/Unit/Product 등 다른 테이블은 절대 건드리지 않음

            migrationBuilder.Sql(@"SET FOREIGN_KEY_CHECKS = 0;");

            // 구/신 스키마 잔재까지 전부 제거 (존재 안 해도 DROP IF EXISTS라 안전)
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS `EventNoticeLocalization`;
DROP TABLE IF EXISTS `EventNotice`;

DROP TABLE IF EXISTS `EventLocalization`;
DROP TABLE IF EXISTS `NoticeLocalization`;
DROP TABLE IF EXISTS `Notice`;

DROP TABLE IF EXISTS `UserEventClaim`;
DROP TABLE IF EXISTS `UserEventProgress`;
DROP TABLE IF EXISTS `EventRewardTier`;

DROP TABLE IF EXISTS `Event`;
DROP TABLE IF EXISTS `EventDefinition`;
");

            migrationBuilder.Sql(@"SET FOREIGN_KEY_CHECKS = 1;");

            // ========== Event ==========
            migrationBuilder.CreateTable(
                name: "Event",
                columns: table => new
                {
                    EventId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),

                    EventKey = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),

                    StartAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EndAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),

                    RepeatType = table.Column<int>(type: "int", nullable: false),
                    RepeatTimezone = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    Version = table.Column<int>(type: "int", nullable: false),

                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),

                    IsPinned = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    Priority = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Event", x => x.EventId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Event_EventKey",
                table: "Event",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Event_IsActive_StartAt_EndAt",
                table: "Event",
                columns: new[] { "IsActive", "StartAt", "EndAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Event_IsActive_IsPinned_Priority_CreatedAt",
                table: "Event",
                columns: new[] { "IsActive", "IsPinned", "Priority", "CreatedAt" });

            // ========== EventLocalization ==========
            migrationBuilder.CreateTable(
                name: "EventLocalization",
                columns: table => new
                {
                    EventLocalizationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),

                    EventId = table.Column<int>(type: "int", nullable: false),

                    LanguageCode = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    Title = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    Content = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventLocalization", x => x.EventLocalizationId);
                    table.ForeignKey(
                        name: "FK_EventLocalization_Event_EventId",
                        column: x => x.EventId,
                        principalTable: "Event",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_EventLocalization_EventId_LanguageCode",
                table: "EventLocalization",
                columns: new[] { "EventId", "LanguageCode" },
                unique: true);

            // ========== EventRewardTier ==========
            migrationBuilder.CreateTable(
                name: "EventRewardTier",
                columns: table => new
                {
                    EventRewardTierId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),

                    EventId = table.Column<int>(type: "int", nullable: false),

                    Tier = table.Column<int>(type: "int", nullable: false),

                    ConditionJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RewardJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),

                    MinEventVersion = table.Column<int>(type: "int", nullable: false),
                    MaxEventVersion = table.Column<int>(type: "int", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventRewardTier", x => x.EventRewardTierId);
                    table.ForeignKey(
                        name: "FK_EventRewardTier_Event_EventId",
                        column: x => x.EventId,
                        principalTable: "Event",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_EventRewardTier_EventId_Tier",
                table: "EventRewardTier",
                columns: new[] { "EventId", "Tier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventRewardTier_EventId_IsActive",
                table: "EventRewardTier",
                columns: new[] { "EventId", "IsActive" });

            // ========== UserEventProgress ==========
            migrationBuilder.CreateTable(
                name: "UserEventProgress",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    EventId = table.Column<int>(type: "int", nullable: false),

                    CycleKey = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    ProgressValue = table.Column<int>(type: "int", nullable: false),

                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEventProgress", x => new { x.UserId, x.EventId, x.CycleKey });

                    table.ForeignKey(
                        name: "FK_UserEventProgress_Event_EventId",
                        column: x => x.EventId,
                        principalTable: "Event",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_UserEventProgress_EventId_CycleKey",
                table: "UserEventProgress",
                columns: new[] { "EventId", "CycleKey" });

            // ========== UserEventClaim ==========
            // Fluent과 동일:
            // PK: (UserId, EventId, Tier, CycleKey)
            // UNIQUE: (UserId, ClaimTxId)
            migrationBuilder.CreateTable(
                name: "UserEventClaim",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    Tier = table.Column<int>(type: "int", nullable: false),

                    CycleKey = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    ClaimTxId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    ClaimedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP(6)"),

                    EventVersionAtClaim = table.Column<int>(type: "int", nullable: false),

                    RewardSnapshotJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEventClaim", x => new { x.UserId, x.EventId, x.Tier, x.CycleKey });

                    table.ForeignKey(
                        name: "FK_UserEventClaim_Event_EventId",
                        column: x => x.EventId,
                        principalTable: "Event",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_UserEventClaim_UserId_ClaimTxId",
                table: "UserEventClaim",
                columns: new[] { "UserId", "ClaimTxId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserEventClaim_EventId_CycleKey",
                table: "UserEventClaim",
                columns: new[] { "EventId", "CycleKey" });

            // ========== Notice ==========
            migrationBuilder.CreateTable(
                name: "Notice",
                columns: table => new
                {
                    NoticeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),

                    IsPinned = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),

                    StartAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EndAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),

                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP(6)"),

                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notice", x => x.NoticeId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Notice_IsActive_IsPinned_CreatedAt",
                table: "Notice",
                columns: new[] { "IsActive", "IsPinned", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notice_IsActive_StartAt_EndAt",
                table: "Notice",
                columns: new[] { "IsActive", "StartAt", "EndAt" });

            // ========== NoticeLocalization ==========
            migrationBuilder.CreateTable(
                name: "NoticeLocalization",
                columns: table => new
                {
                    NoticeLocalizationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),

                    NoticeId = table.Column<int>(type: "int", nullable: false),

                    LanguageCode = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    Title = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    Content = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoticeLocalization", x => x.NoticeLocalizationId);
                    table.ForeignKey(
                        name: "FK_NoticeLocalization_Notice_NoticeId",
                        column: x => x.NoticeId,
                        principalTable: "Notice",
                        principalColumn: "NoticeId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_NoticeLocalization_NoticeId_LanguageCode",
                table: "NoticeLocalization",
                columns: new[] { "NoticeId", "LanguageCode" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 로컬 전용: 되돌리기 필요 없으면 최소화.
            migrationBuilder.Sql(@"SET FOREIGN_KEY_CHECKS = 0;");

            migrationBuilder.DropTable(name: "EventLocalization");
            migrationBuilder.DropTable(name: "NoticeLocalization");

            migrationBuilder.DropTable(name: "UserEventClaim");
            migrationBuilder.DropTable(name: "UserEventProgress");
            migrationBuilder.DropTable(name: "EventRewardTier");

            migrationBuilder.DropTable(name: "Notice");
            migrationBuilder.DropTable(name: "Event");

            migrationBuilder.Sql(@"SET FOREIGN_KEY_CHECKS = 1;");
        }
    }
}