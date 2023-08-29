using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coflnet.SongVoter.Migrations
{
    public partial class AddLookup : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Lookup",
                table: "Songs",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupportedPlatforms",
                table: "Parties",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "PlayCounter",
                table: "ExternalSongs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_Songs_Lookup",
                table: "Songs",
                column: "Lookup");

            // set lookup to title + artist lowercase without spaces and special characters
            migrationBuilder.Sql("UPDATE \"Songs\" SET \"Lookup\" = LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(\"Title\", ' ', ''), '!', ''), '?', ''), '.', ''), ',', ''), ';', ''), ':', ''), '-', ''), '_', ''), '(', ''), ')', ''), '[', ''), ']', '')) || LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(\"Artist\", ' ', ''), '!', ''), '?', ''), '.', ''), ',', ''), ';', ''), ':', ''), '-', ''), '_', ''), '(', ''), ')', ''), '[', ''), ']', ''))");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Songs_Lookup",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "Lookup",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "SupportedPlatforms",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "PlayCounter",
                table: "ExternalSongs");
        }
    }
}
