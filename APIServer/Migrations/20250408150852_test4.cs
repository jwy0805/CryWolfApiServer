using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiServer.Migrations
{
    /// <inheritdoc />
    public partial class test4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Dummy1",
                table: "User");

            migrationBuilder.DropColumn(
                name: "Dummy2",
                table: "User");

            migrationBuilder.DropColumn(
                name: "Dummy3",
                table: "User");

            migrationBuilder.DropColumn(
                name: "LastPingTime",
                table: "User");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Dummy1",
                table: "User",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Dummy2",
                table: "User",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "Dummy3",
                table: "User",
                type: "datetime(6)",
                nullable: true);
            
            migrationBuilder.AddColumn<DateTime>(
                name: "LastPingTime",
                table: "User",
                type: "datetime(6)",
                nullable: true);
        }
    }
}
