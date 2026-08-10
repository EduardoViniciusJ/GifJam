using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GifJam.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscordId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AvatarUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "auth_exchange_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auth_exchange_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_auth_exchange_codes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character(5)", fixedLength: true, maxLength: 5, nullable: false),
                    HostUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TotalRounds = table.Column<int>(type: "integer", nullable: false),
                    CurrentRoundNumber = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_games", x => x.Id);
                    table.ForeignKey(
                        name: "FK_games_users_HostUserId",
                        column: x => x.HostUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "game_players",
                columns: table => new
                {
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    IsReady = table.Column<bool>(type: "boolean", nullable: false),
                    IsConnected = table.Column<bool>(type: "boolean", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_players", x => new { x.GameId, x.UserId });
                    table.ForeignKey(
                        name: "FK_game_players_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_game_players_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "gif_submissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PreviewUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    MediaUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Attribution = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gif_submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gif_submissions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "gif_votes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    GifSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gif_votes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gif_votes_gif_submissions_GifSubmissionId",
                        column: x => x.GifSubmissionId,
                        principalTable: "gif_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_gif_votes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "phrase_votes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhraseId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_phrase_votes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_phrase_votes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "phrases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_phrases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_phrases_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundNumber = table.Column<int>(type: "integer", nullable: false),
                    Phase = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    SelectedPhraseId = table.Column<Guid>(type: "uuid", nullable: true),
                    PhaseEndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rounds_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_rounds_phrases_SelectedPhraseId",
                        column: x => x.SelectedPhraseId,
                        principalTable: "phrases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_auth_exchange_codes_CodeHash",
                table: "auth_exchange_codes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_auth_exchange_codes_ExpiresAt",
                table: "auth_exchange_codes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_auth_exchange_codes_UserId",
                table: "auth_exchange_codes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_game_players_UserId",
                table: "game_players",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_games_Code",
                table: "games",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_games_HostUserId",
                table: "games",
                column: "HostUserId");

            migrationBuilder.CreateIndex(
                name: "IX_games_Status_CreatedAt",
                table: "games",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_gif_submissions_RoundId_UserId",
                table: "gif_submissions",
                columns: new[] { "RoundId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gif_submissions_UserId",
                table: "gif_submissions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_gif_votes_GifSubmissionId",
                table: "gif_votes",
                column: "GifSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_gif_votes_RoundId_UserId",
                table: "gif_votes",
                columns: new[] { "RoundId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gif_votes_UserId",
                table: "gif_votes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_phrase_votes_PhraseId",
                table: "phrase_votes",
                column: "PhraseId");

            migrationBuilder.CreateIndex(
                name: "IX_phrase_votes_RoundId_UserId",
                table: "phrase_votes",
                columns: new[] { "RoundId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_phrase_votes_UserId",
                table: "phrase_votes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_phrases_RoundId_UserId",
                table: "phrases",
                columns: new[] { "RoundId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_phrases_UserId",
                table: "phrases",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_rounds_GameId_RoundNumber",
                table: "rounds",
                columns: new[] { "GameId", "RoundNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rounds_Phase_PhaseEndsAt",
                table: "rounds",
                columns: new[] { "Phase", "PhaseEndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_rounds_SelectedPhraseId",
                table: "rounds",
                column: "SelectedPhraseId");

            migrationBuilder.CreateIndex(
                name: "IX_users_DiscordId",
                table: "users",
                column: "DiscordId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_gif_submissions_rounds_RoundId",
                table: "gif_submissions",
                column: "RoundId",
                principalTable: "rounds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_gif_votes_rounds_RoundId",
                table: "gif_votes",
                column: "RoundId",
                principalTable: "rounds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_phrase_votes_phrases_PhraseId",
                table: "phrase_votes",
                column: "PhraseId",
                principalTable: "phrases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_phrase_votes_rounds_RoundId",
                table: "phrase_votes",
                column: "RoundId",
                principalTable: "rounds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_phrases_rounds_RoundId",
                table: "phrases",
                column: "RoundId",
                principalTable: "rounds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_games_users_HostUserId",
                table: "games");

            migrationBuilder.DropForeignKey(
                name: "FK_phrases_users_UserId",
                table: "phrases");

            migrationBuilder.DropForeignKey(
                name: "FK_rounds_games_GameId",
                table: "rounds");

            migrationBuilder.DropForeignKey(
                name: "FK_phrases_rounds_RoundId",
                table: "phrases");

            migrationBuilder.DropTable(
                name: "auth_exchange_codes");

            migrationBuilder.DropTable(
                name: "game_players");

            migrationBuilder.DropTable(
                name: "gif_votes");

            migrationBuilder.DropTable(
                name: "phrase_votes");

            migrationBuilder.DropTable(
                name: "gif_submissions");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "games");

            migrationBuilder.DropTable(
                name: "rounds");

            migrationBuilder.DropTable(
                name: "phrases");
        }
    }
}
