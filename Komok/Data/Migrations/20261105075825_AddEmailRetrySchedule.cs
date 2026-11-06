using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Komok.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailRetrySchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailOutbox_SentAt_CreatedAt",
                table: "EmailOutbox");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                table: "EmailOutbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_SentAt_NextAttemptAt_CreatedAt",
                table: "EmailOutbox",
                columns: new[] { "SentAt", "NextAttemptAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailOutbox_SentAt_NextAttemptAt_CreatedAt",
                table: "EmailOutbox");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "EmailOutbox");

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_SentAt_CreatedAt",
                table: "EmailOutbox",
                columns: new[] { "SentAt", "CreatedAt" });
        }
    }
}
