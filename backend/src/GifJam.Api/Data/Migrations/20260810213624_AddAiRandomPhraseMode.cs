using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GifJam.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiRandomPhraseMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TotalScore",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE users AS user_record
                SET "TotalScore" = COALESCE((
                    SELECT SUM(game_player."Score")
                    FROM game_players AS game_player
                    WHERE game_player."UserId" = user_record."Id"
                ), 0);
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "phrases",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "phrases",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Player");

            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "games",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "Classic");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalScore",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "phrases");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "games");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "phrases",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
