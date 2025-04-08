using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountServer.Migrations
{
    /// <inheritdoc />
    public partial class stage_reward_new_column_added : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Star",
                table: "Stage_Reward",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Star",
                table: "Stage_Reward");
        }
    }
}
