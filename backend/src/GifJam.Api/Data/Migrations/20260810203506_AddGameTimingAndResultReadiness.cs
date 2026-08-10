using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GifJam.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameTimingAndResultReadiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GifVotingPresentationEndsAt",
                table: "rounds",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PhraseSubmissionSeconds",
                table: "games",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<int>(
                name: "ResultsSeconds",
                table: "games",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<int>(
                name: "ResultReadyRoundNumber",
                table: "game_players",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GifVotingPresentationEndsAt",
                table: "rounds");

            migrationBuilder.DropColumn(
                name: "PhraseSubmissionSeconds",
                table: "games");

            migrationBuilder.DropColumn(
                name: "ResultsSeconds",
                table: "games");

            migrationBuilder.DropColumn(
                name: "ResultReadyRoundNumber",
                table: "game_players");
        }
    }
}
