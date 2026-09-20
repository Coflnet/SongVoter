using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coflnet.SongVoter.Migrations
{
    /// <inheritdoc />
    public partial class WeightedParties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PartySongs_PartyId",
                table: "PartySongs");

            migrationBuilder.AlterColumn<int>(
                name: "PlayedTimes",
                table: "PartySongs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint");

            migrationBuilder.AddColumn<int>(
                name: "CurrentSongId",
                table: "Parties",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlaybackVersion",
                table: "Parties",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Invites",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.Sql(LegacyQueue.MergeDuplicates);

            migrationBuilder.CreateIndex(
                name: "IX_PartySongs_PartyId_SongId",
                table: "PartySongs",
                columns: new[] { "PartyId", "SongId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invites_Code",
                table: "Invites",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PartySongs_PartyId_SongId",
                table: "PartySongs");

            migrationBuilder.DropIndex(
                name: "IX_Invites_Code",
                table: "Invites");

            migrationBuilder.DropColumn(
                name: "CurrentSongId",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "PlaybackVersion",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Invites");

            migrationBuilder.AlterColumn<short>(
                name: "PlayedTimes",
                table: "PartySongs",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_PartySongs_PartyId",
                table: "PartySongs",
                column: "PartyId");
        }
    }
}
