using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coflnet.SongVoter.Migrations
{
    public partial class invite : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalSong_Songs_SongId",
                table: "ExternalSong");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ExternalSong",
                table: "ExternalSong");

            migrationBuilder.RenameTable(
                name: "ExternalSong",
                newName: "ExternalSongs");

            migrationBuilder.RenameIndex(
                name: "IX_ExternalSong_SongId",
                table: "ExternalSongs",
                newName: "IX_ExternalSongs_SongId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ExternalSongs",
                table: "ExternalSongs",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Parties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CreatorId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "varchar(30) CHARACTER SET utf8mb4", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parties_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PartyId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    CreatorId = table.Column<int>(type: "int", nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invites_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invites_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PartySongs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PartyId = table.Column<int>(type: "int", nullable: false),
                    PlayedTimes = table.Column<short>(type: "smallint", nullable: false),
                    SongId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartySongs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartySongs_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartySongs_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartyUser",
                columns: table => new
                {
                    MembersId = table.Column<int>(type: "int", nullable: false),
                    PartiesId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyUser", x => new { x.MembersId, x.PartiesId });
                    table.ForeignKey(
                        name: "FK_PartyUser_Parties_PartiesId",
                        column: x => x.PartiesId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartyUser_Users_MembersId",
                        column: x => x.MembersId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartySongUser",
                columns: table => new
                {
                    DownVotersId = table.Column<int>(type: "int", nullable: false),
                    DownvotesId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartySongUser", x => new { x.DownVotersId, x.DownvotesId });
                    table.ForeignKey(
                        name: "FK_PartySongUser_PartySongs_DownvotesId",
                        column: x => x.DownvotesId,
                        principalTable: "PartySongs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartySongUser_Users_DownVotersId",
                        column: x => x.DownVotersId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartySongUser1",
                columns: table => new
                {
                    UpVotersId = table.Column<int>(type: "int", nullable: false),
                    UpvotesId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartySongUser1", x => new { x.UpVotersId, x.UpvotesId });
                    table.ForeignKey(
                        name: "FK_PartySongUser1_PartySongs_UpvotesId",
                        column: x => x.UpvotesId,
                        principalTable: "PartySongs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartySongUser1_Users_UpVotersId",
                        column: x => x.UpVotersId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invites_PartyId",
                table: "Invites",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_Invites_UserId",
                table: "Invites",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Parties_CreatorId",
                table: "Parties",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_PartySongs_PartyId",
                table: "PartySongs",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_PartySongs_SongId",
                table: "PartySongs",
                column: "SongId");

            migrationBuilder.CreateIndex(
                name: "IX_PartySongUser_DownvotesId",
                table: "PartySongUser",
                column: "DownvotesId");

            migrationBuilder.CreateIndex(
                name: "IX_PartySongUser1_UpvotesId",
                table: "PartySongUser1",
                column: "UpvotesId");

            migrationBuilder.CreateIndex(
                name: "IX_PartyUser_PartiesId",
                table: "PartyUser",
                column: "PartiesId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalSongs_Songs_SongId",
                table: "ExternalSongs",
                column: "SongId",
                principalTable: "Songs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalSongs_Songs_SongId",
                table: "ExternalSongs");

            migrationBuilder.DropTable(
                name: "Invites");

            migrationBuilder.DropTable(
                name: "PartySongUser");

            migrationBuilder.DropTable(
                name: "PartySongUser1");

            migrationBuilder.DropTable(
                name: "PartyUser");

            migrationBuilder.DropTable(
                name: "PartySongs");

            migrationBuilder.DropTable(
                name: "Parties");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ExternalSongs",
                table: "ExternalSongs");

            migrationBuilder.RenameTable(
                name: "ExternalSongs",
                newName: "ExternalSong");

            migrationBuilder.RenameIndex(
                name: "IX_ExternalSongs_SongId",
                table: "ExternalSong",
                newName: "IX_ExternalSong_SongId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ExternalSong",
                table: "ExternalSong",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalSong_Songs_SongId",
                table: "ExternalSong",
                column: "SongId",
                principalTable: "Songs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
