using System;
using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260720130000_AddTicketReminders")]
    public partial class AddTicketReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ─── GlobalSettings: parametri del preavviso ─────────────────────────
            migrationBuilder.AddColumn<bool>(
                name: "TicketReminderEnabled",
                table: "GlobalSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "TicketAppointmentReminderMinutes",
                table: "GlobalSettings",
                type: "int",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<int>(
                name: "TicketExpiryReminderMinutes",
                table: "GlobalSettings",
                type: "int",
                nullable: false,
                defaultValue: 120);

            // ─── GlobalSettings: default preavviso attività per tipo (nullable) ──
            migrationBuilder.AddColumn<int>(name: "ActivityReminderMinutesCall", table: "GlobalSettings", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>(name: "ActivityReminderMinutesEmail", table: "GlobalSettings", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>(name: "ActivityReminderMinutesMeeting", table: "GlobalSettings", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>(name: "ActivityReminderMinutesTask", table: "GlobalSettings", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>(name: "ActivityReminderMinutesNote", table: "GlobalSettings", type: "int", nullable: true);

            // ─── TicketTypes: override preavviso per tipo (nullable = usa globale) ─
            migrationBuilder.AddColumn<int>(name: "AppointmentReminderMinutes", table: "TicketTypes", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>(name: "ExpiryReminderMinutes", table: "TicketTypes", type: "int", nullable: true);

            // ─── Tickets: stato di consegna dei due preavvisi ────────────────────
            migrationBuilder.AddColumn<int>(
                name: "ReminderApptStatus",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReminderApptRetryCount",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderApptLastAttemptAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReminderExpiryStatus",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReminderExpiryRetryCount",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderExpiryLastAttemptAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReminderLastError",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: true);

            // Marca i ticket già esistenti come "gia' gestiti" (Sent = 1) così il motore
            // dei preavvisi non invia notifiche retroattive per appuntamenti/scadenze passati.
            migrationBuilder.Sql(
                "UPDATE [Tickets] SET [ReminderApptStatus] = 1, [ReminderExpiryStatus] = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ReminderLastError", table: "Tickets");
            migrationBuilder.DropColumn(name: "ReminderExpiryLastAttemptAt", table: "Tickets");
            migrationBuilder.DropColumn(name: "ReminderExpiryRetryCount", table: "Tickets");
            migrationBuilder.DropColumn(name: "ReminderExpiryStatus", table: "Tickets");
            migrationBuilder.DropColumn(name: "ReminderApptLastAttemptAt", table: "Tickets");
            migrationBuilder.DropColumn(name: "ReminderApptRetryCount", table: "Tickets");
            migrationBuilder.DropColumn(name: "ReminderApptStatus", table: "Tickets");

            migrationBuilder.DropColumn(name: "ExpiryReminderMinutes", table: "TicketTypes");
            migrationBuilder.DropColumn(name: "AppointmentReminderMinutes", table: "TicketTypes");

            migrationBuilder.DropColumn(name: "ActivityReminderMinutesNote", table: "GlobalSettings");
            migrationBuilder.DropColumn(name: "ActivityReminderMinutesTask", table: "GlobalSettings");
            migrationBuilder.DropColumn(name: "ActivityReminderMinutesMeeting", table: "GlobalSettings");
            migrationBuilder.DropColumn(name: "ActivityReminderMinutesEmail", table: "GlobalSettings");
            migrationBuilder.DropColumn(name: "ActivityReminderMinutesCall", table: "GlobalSettings");

            migrationBuilder.DropColumn(name: "TicketExpiryReminderMinutes", table: "GlobalSettings");
            migrationBuilder.DropColumn(name: "TicketAppointmentReminderMinutes", table: "GlobalSettings");
            migrationBuilder.DropColumn(name: "TicketReminderEnabled", table: "GlobalSettings");
        }
    }
}
