using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GifJam.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGifMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "gif_submissions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "gif_submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PreviewHeight",
                table: "gif_submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PreviewWidth",
                table: "gif_submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "gif_submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "gif_submissions");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "gif_submissions");

            migrationBuilder.DropColumn(
                name: "PreviewHeight",
                table: "gif_submissions");

            migrationBuilder.DropColumn(
                name: "PreviewWidth",
                table: "gif_submissions");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "gif_submissions");
        }
    }
}
