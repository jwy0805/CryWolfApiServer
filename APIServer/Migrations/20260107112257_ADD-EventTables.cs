using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiServer.Migrations
{
    /// <inheritdoc />
    public partial class ADDEventTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ------------------------------------------------------------------
            // 1) EventNoticeLocalization index 교체 (FK 때문에 순서 중요)
            // ------------------------------------------------------------------
            // FK가 EventNoticeId에 대한 인덱스를 요구할 수 있으므로 단독 인덱스를 먼저 만든다.
            migrationBuilder.CreateIndex(
                name: "IX_EventNoticeLocalization_EventNoticeId",
                table: "EventNoticeLocalization",
                column: "EventNoticeId");

            // 기존 인덱스 Drop (이제 FK가 단독 인덱스를 사용할 수 있음)
            migrationBuilder.DropIndex(
                name: "IX_EventNoticeLocalization_EventNoticeId_LanguageCode",
                table: "EventNoticeLocalization");

            // Unique 복합 인덱스는 새 이름으로 생성 (기존 이름과 충돌 방지)
            migrationBuilder.CreateIndex(
                name: "UX_EventNoticeLocalization_EventNoticeId_LanguageCode",
                table: "EventNoticeLocalization",
                columns: new[] { "EventNoticeId", "LanguageCode" },
                unique: true);

            // ------------------------------------------------------------------
            // 2) EventNotice index 교체 + EventId 컬럼 추가
            // ------------------------------------------------------------------
            migrationBuilder.DropIndex(
                name: "IX_EventNotice_IsActive_NoticeType_CreatedAt",
                table: "EventNotice");

            migrationBuilder.AddColumn<int>(
                name: "EventId",
                table: "EventNotice",
                type: "int",
                nullable: true);

            // ------------------------------------------------------------------
            // 3) 신규 테이블 생성
            // ------------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "EventDefinition",
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
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    CreatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventDefinition", x => x.EventId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
                    MaxEventVersion = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventRewardTier", x => x.EventRewardTierId);
                    table.ForeignKey(
                        name: "FK_EventRewardTier_EventDefinition_EventId",
                        column: x => x.EventId,
                        principalTable: "EventDefinition",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
                    ClaimedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    EventVersionAtClaim = table.Column<int>(type: "int", nullable: false),
                    RewardSnapshotJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEventClaim", x => new { x.UserId, x.EventId, x.Tier, x.CycleKey });
                    table.ForeignKey(
                        name: "FK_UserEventClaim_EventDefinition_EventId",
                        column: x => x.EventId,
                        principalTable: "EventDefinition",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserEventProgress",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    CycleKey = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProgressValue = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEventProgress", x => new { x.UserId, x.EventId, x.CycleKey });
                    table.ForeignKey(
                        name: "FK_UserEventProgress_EventDefinition_EventId",
                        column: x => x.EventId,
                        principalTable: "EventDefinition",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // ------------------------------------------------------------------
            // 4) 신규 인덱스 생성
            // ------------------------------------------------------------------
            migrationBuilder.CreateIndex(
                name: "IX_EventNotice_EventId",
                table: "EventNotice",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_EventNotice_IsActive_NoticeType_IsPinned_CreatedAt",
                table: "EventNotice",
                columns: new[] { "IsActive", "NoticeType", "IsPinned", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EventDefinition_EventKey",
                table: "EventDefinition",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventDefinition_IsActive_StartAt_EndAt",
                table: "EventDefinition",
                columns: new[] { "IsActive", "StartAt", "EndAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EventRewardTier_EventId_IsActive",
                table: "EventRewardTier",
                columns: new[] { "EventId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EventRewardTier_EventId_Tier",
                table: "EventRewardTier",
                columns: new[] { "EventId", "Tier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserEventClaim_EventId",
                table: "UserEventClaim",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_UserEventClaim_UserId_ClaimTxId",
                table: "UserEventClaim",
                columns: new[] { "UserId", "ClaimTxId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserEventProgress_EventId",
                table: "UserEventProgress",
                column: "EventId");

            // ------------------------------------------------------------------
            // 5) FK: EventNotice -> EventDefinition (SetNull)
            // ------------------------------------------------------------------
            migrationBuilder.AddForeignKey(
                name: "FK_EventNotice_EventDefinition_EventId",
                table: "EventNotice",
                column: "EventId",
                principalTable: "EventDefinition",
                principalColumn: "EventId",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ------------------------------------------------------------------
            // 1) FK 제거
            // ------------------------------------------------------------------
            migrationBuilder.DropForeignKey(
                name: "FK_EventNotice_EventDefinition_EventId",
                table: "EventNotice");

            // ------------------------------------------------------------------
            // 2) 신규 테이블 제거
            // ------------------------------------------------------------------
            migrationBuilder.DropTable(name: "EventRewardTier");
            migrationBuilder.DropTable(name: "UserEventClaim");
            migrationBuilder.DropTable(name: "UserEventProgress");
            migrationBuilder.DropTable(name: "EventDefinition");

            // ------------------------------------------------------------------
            // 3) EventNotice 인덱스/컬럼 복구
            // ------------------------------------------------------------------
            migrationBuilder.DropIndex(
                name: "IX_EventNotice_EventId",
                table: "EventNotice");

            migrationBuilder.DropIndex(
                name: "IX_EventNotice_IsActive_NoticeType_IsPinned_CreatedAt",
                table: "EventNotice");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "EventNotice");

            migrationBuilder.CreateIndex(
                name: "IX_EventNotice_IsActive_NoticeType_CreatedAt",
                table: "EventNotice",
                columns: new[] { "IsActive", "NoticeType", "CreatedAt" });

            // ------------------------------------------------------------------
            // 4) EventNoticeLocalization 인덱스 복구 (Up의 역순)
            // ------------------------------------------------------------------
            migrationBuilder.DropIndex(
                name: "UX_EventNoticeLocalization_EventNoticeId_LanguageCode",
                table: "EventNoticeLocalization");

            migrationBuilder.CreateIndex(
                name: "IX_EventNoticeLocalization_EventNoticeId_LanguageCode",
                table: "EventNoticeLocalization",
                columns: new[] { "EventNoticeId", "LanguageCode" });

            // Up에서 만든 단독 인덱스 제거
            migrationBuilder.DropIndex(
                name: "IX_EventNoticeLocalization_EventNoticeId",
                table: "EventNoticeLocalization");
        }
    }
}
