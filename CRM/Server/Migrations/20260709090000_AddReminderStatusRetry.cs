using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderStatusRetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Activities_ReminderSent_ReminderAt",
                table: "Activities");

            migrationBuilder.AddColumn<int>(
                name: "ReminderStatus",
                table: "Activities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReminderRetryCount",
                table: "Activities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderLastAttemptAt",
                table: "Activities",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReminderLastError",
                table: "Activities",
                type: "nvarchar(max)",
                nullable: true);

            // Migrazione dati: i promemoria gia' inviati diventano Sent (1); gli altri restano Pending (0).
            migrationBuilder.Sql("UPDATE [Activities] SET [ReminderStatus] = 1 WHERE [ReminderSent] = 1;");

            migrationBuilder.DropColumn(
                name: "ReminderSent",
                table: "Activities");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_ReminderStatus_ReminderAt",
                table: "Activities",
                columns: new[] { "ReminderStatus", "ReminderAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Activities_ReminderStatus_ReminderAt",
                table: "Activities");

            migrationBuilder.AddColumn<bool>(
                name: "ReminderSent",
                table: "Activities",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Ripristino dati: consideriamo "inviati" i promemoria in stato Sent (1).
            migrationBuilder.Sql("UPDATE [Activities] SET [ReminderSent] = 1 WHERE [ReminderStatus] = 1;");

            migrationBuilder.DropColumn(
                name: "ReminderStatus",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "ReminderRetryCount",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "ReminderLastAttemptAt",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "ReminderLastError",
                table: "Activities");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_ReminderSent_ReminderAt",
                table: "Activities",
                columns: new[] { "ReminderSent", "ReminderAt" });
        }
    }
}
