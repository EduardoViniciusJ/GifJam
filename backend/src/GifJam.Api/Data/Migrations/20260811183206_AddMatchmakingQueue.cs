using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GifJam.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchmakingQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "matchmaking_batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeadlineAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GameId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matchmaking_batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_matchmaking_batches_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "matchmaking_tickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matchmaking_tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_matchmaking_tickets_matchmaking_batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "matchmaking_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_matchmaking_tickets_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_matchmaking_batches_GameId",
                table: "matchmaking_batches",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_matchmaking_batches_Status_DeadlineAt",
                table: "matchmaking_batches",
                columns: new[] { "Status", "DeadlineAt" });

            migrationBuilder.CreateIndex(
                name: "IX_matchmaking_tickets_BatchId_JoinedAt",
                table: "matchmaking_tickets",
                columns: new[] { "BatchId", "JoinedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_matchmaking_tickets_UserId_Status",
                table: "matchmaking_tickets",
                columns: new[] { "UserId", "Status" },
                unique: true,
                filter: "\"Status\" = 'Waiting'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "matchmaking_tickets");

            migrationBuilder.DropTable(
                name: "matchmaking_batches");
        }
    }
}
