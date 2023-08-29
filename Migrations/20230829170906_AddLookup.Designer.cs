﻿// <auto-generated />
using System;
using Coflnet.SongVoter.DBModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Coflnet.SongVoter.Migrations
{
    [DbContext(typeof(SVContext))]
    [Migration("20230829170906_AddLookup")]
    partial class AddLookup
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.7")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Coflnet.SongVoter.DBModels.ExternalSong", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("Artist")
                        .HasColumnType("text");

                    b.Property<TimeSpan>("Duration")
                        .HasColumnType("interval");

                    b.Property<string>("ExternalId")
                        .HasColumnType("text");

                    b.Property<int>("Platform")
                        .HasColumnType("integer");

                    b.Property<long>("PlayCounter")
                        .HasColumnType("bigint");

                    b.Property<int?>("SongId")
                        .HasColumnType("integer");

                    b.Property<string>("ThumbnailUrl")
                        .HasColumnType("text");

                    b.Property<string>("Title")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("SongId");

                    b.ToTable("ExternalSongs");
                });

            modelBuilder.Entity("Coflnet.SongVoter.DBModels.Invite", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<int>("CreatorId")
                        .HasColumnType("integer");

                    b.Property<int?>("PartyId")
                        .HasColumnType("integer");

                    b.Property<int>("UsageCount")
                        .HasColumnType("integer");

                    b.Property<int>("UsageLimit")
                        .HasColumnType("integer");

                    b.Property<int?>("UserId")
                        .HasColumnType("integer");

                    b.Property<DateTime>("ValidUntil")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("PartyId");

                    b.HasIndex("UserId");

                    b.ToTable("Invites");
                });

            modelBuilder.Entity("Coflnet.SongVoter.DBModels.Oauth2Token", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("AccessToken")
                        .HasColumnType("text");

                    b.Property<string>("AuthCode")
                        .HasColumnType("text");

                    b.Property<DateTime>("Expiration")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ExternalId")
                        .HasColumnType("text");

                    b.Property<int>("Platform")
                        .HasColumnType("integer");

                    b.Property<string>("RefreshToken")
                        .HasColumnType("text");

                    b.Property<string>("Scropes")
                        .HasColumnType("text");

                    b.Property<int?>("UserId")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("Oauth2Token");
                });

            modelBuilder.Entity("Coflnet.SongVoter.DBModels.Party", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<int?>("CreatorId")
                        .HasColumnType("integer");

                    b.Property<string>("Name")
                        .HasMaxLength(30)
                        .HasColumnType("character varying(30)");

                    b.Property<int>("SupportedPlatforms")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("CreatorId");

                    b.ToTable("Parties");
                });

            modelBuilder.Entity("Coflnet.SongVoter.DBModels.PartySong", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<int>("PartyId")
                        .HasColumnType("integer");

                    b.Property<short>("PlayedTimes")
                        .HasColumnType("smallint");

                    b.Property<int>("SongId")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("PartyId");

                    b.HasIndex("SongId");

                    b.ToTable("PartySongs");
                });

            modelBuilder.Entity("Coflnet.SongVoter.DBModels.Playlist", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<int>("Owner")
                        .HasColumnType("integer");

                    b.Property<string>("Title")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("PlayLists");
                });

            modelBuilder.Entity("Coflnet.SongVoter.DBModels.Song", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("Lookup")
                        .HasMaxLength(200)
                        .HasColumnType("varchar(200)");

                    b.Property<string>("Title")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("Lookup");

                    b.ToTable("Songs");
                });

            modelBuilder.Entity("Coflnet.SongVoter.DBModels.User", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("GoogleId")
                        .HasColumnType("text");

                    b.Property<string>("Name")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("PartySongUser", b =>
                {
                    b.Property<int>("DownVotersId")
                        .HasColumnType("integer");

                    b.Property<long>("DownvotesId")
                        .HasColumnType("bigint");

                    b.HasKey("DownVotersId", "DownvotesId");

                    b.HasIndex("DownvotesId");

                    b.ToTable("PartySongUser");
                });

            modelBuilder.Entity("PartySongUser1", b =>
                {
                    b.Property<int>("UpVotersId")
                        .HasColumnType("integer");

                    b.Property<long>("UpvotesId")
                        .HasColumnType("bigint");

                    b.HasKey("UpVotersId", "UpvotesId");

                    b.HasIndex("UpvotesId");

                    b.ToTable("PartySongUser1");
                });

            modelBuilder.Entity("PartyUser", b =>
                {
                    b.Property<int>("MembersId")
                        .HasColumnType("integer");

                    b.Property<int>("PartiesId")
                        .HasColumnType("integer");

                    b.HasKey("MembersId", "PartiesId");

                    b.HasIndex("PartiesId");

                    b.ToTable("PartyUser");
                });

            modelBuilder.Entity("PlaylistSong", b =>
                {
                    b.Property<int>("PlaylistsId")
                        .HasColumnType("integer");

                    b.Property<int>("SongsId")
                        .HasColumnType("integer");

                    b.HasKey("PlaylistsId", "SongsId");

                    b.HasIndex("SongsId");

                    b.ToTable("PlaylistSong");
                });

            modelBuilder.Entity("Coflnet.SongVoter.DBModels.ExternalSong", b =>
                {
                    b.HasOne("Coflnet.SongVoter.DBModels.Song", null)
                        .WithMany("ExternalSongs")
                        .HasForeignKey("SongId");
                });

            modelBuilder.Entity("Coflnet.SongVoter.DBModels.Invite", b =>
                {
                    b.HasOne("Coflnet.SongVoter.DBModels.Party", "Party")
                        .WithMany()
                        .HasForeignKey("PartyId");

                    b.HasOne("Coflnet.SongVoter.DBModels.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId");

                    b.Navigation("Party");

                    b.Navigation("User");
                });

            modelBuilder.Entity("Coflnet.SongVoter.DBModels.Oauth2Token", b =>
                {
                    b.HasOne("Coflnet.SongVoter.DBModels.User", "User")
                        .WithMany("Tokens")
                        .HasForeignKey("UserId");

                    b.Navigation("User");
                });

            modelBuilder.Entity("Coflnet.SongVoter.DBModels.Party", b =>
                {
                    b.HasOne("Coflnet.SongVoter.DBModels.User", "Creator")
                        .WithMany()
                        .HasForeignKey("CreatorId");

                    b.Navigation("Creator");
                });

            modelBuilder.Entity("Coflnet.SongVoter.DBModels.PartySong", b =>
                {
                    b.HasOne("Coflnet.SongVoter.DBModels.Party", "Party")
                        .WithMany("Songs")
                        .HasForeignKey("PartyId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Coflnet.SongVoter.DBModels.Song", "Song")
                        .WithMany()
                        .HasForeignKey("SongId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Party");

                    b.Navigation("Song");
                });

            modelBuilder.Entity("PartySongUser", b =>
                {
                    b.HasOne("Coflnet.SongVoter.DBModels.User", null)
                        .WithMany()
                        .HasForeignKey("DownVotersId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Coflnet.SongVoter.DBModels.PartySong", null)
                        .WithMany()
                        .HasForeignKey("DownvotesId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("PartySongUser1", b =>
                {
                    b.HasOne("Coflnet.SongVoter.DBModels.User", null)
                        .WithMany()
                        .HasForeignKey("UpVotersId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Coflnet.SongVoter.DBModels.PartySong", null)
                        .WithMany()
                        .HasForeignKey("UpvotesId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("PartyUser", b =>
                {
                    b.HasOne("Coflnet.SongVoter.DBModels.User", null)
                        .WithMany()
                        .HasForeignKey("MembersId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Coflnet.SongVoter.DBModels.Party", null)
                        .WithMany()
                        .HasForeignKey("PartiesId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("PlaylistSong", b =>
                {
                    b.HasOne("Coflnet.SongVoter.DBModels.Playlist", null)
                        .WithMany()
                        .HasForeignKey("PlaylistsId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Coflnet.SongVoter.DBModels.Song", null)
                        .WithMany()
                        .HasForeignKey("SongsId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Coflnet.SongVoter.DBModels.Party", b =>
                {
                    b.Navigation("Songs");
                });

            modelBuilder.Entity("Coflnet.SongVoter.DBModels.Song", b =>
                {
                    b.Navigation("ExternalSongs");
                });

            modelBuilder.Entity("Coflnet.SongVoter.DBModels.User", b =>
                {
                    b.Navigation("Tokens");
                });
#pragma warning restore 612, 618
        }
    }
}
