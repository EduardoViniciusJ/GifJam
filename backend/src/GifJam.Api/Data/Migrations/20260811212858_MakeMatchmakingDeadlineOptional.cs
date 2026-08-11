using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GifJam.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeMatchmakingDeadlineOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DeadlineAt",
                table: "matchmaking_batches",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.Sql(
                """
                UPDATE matchmaking_batches AS batch
                SET "DeadlineAt" = CASE
                    WHEN (
                        SELECT COUNT(*)
                        FROM matchmaking_tickets AS ticket
                        WHERE ticket."BatchId" = batch."Id"
                          AND ticket."Status" = 'Waiting'
                    ) < 2 THEN NULL
                    WHEN (
                        SELECT COUNT(*)
                        FROM matchmaking_tickets AS ticket
                        WHERE ticket."BatchId" = batch."Id"
                          AND ticket."Status" = 'Waiting'
                    ) < 6 THEN NOW() + INTERVAL '30 seconds'
                    ELSE NOW()
                END
                WHERE batch."Status" = 'Waiting';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DeadlineAt",
                table: "matchmaking_batches",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
