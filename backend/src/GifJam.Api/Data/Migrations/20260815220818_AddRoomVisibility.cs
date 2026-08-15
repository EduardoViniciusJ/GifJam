using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GifJam.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                table: "games",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Private");

            migrationBuilder.CreateIndex(
                name: "IX_games_Visibility_Status_CreatedAt",
                table: "games",
                columns: new[] { "Visibility", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_games_Visibility_Status_CreatedAt",
                table: "games");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "games");
        }
    }
}
