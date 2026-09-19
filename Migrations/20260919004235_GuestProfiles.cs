using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coflnet.SongVoter.Migrations
{
    /// <inheritdoc />
    public partial class GuestProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceKeyHash",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuthChallenges",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdentityHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthChallenges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_DeviceKeyHash",
                table: "Users",
                column: "DeviceKeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuthChallenges_ExpiresAt",
                table: "AuthChallenges",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthChallenges");

            migrationBuilder.DropIndex(
                name: "IX_Users_DeviceKeyHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeviceKeyHash",
                table: "Users");
        }
    }
}
