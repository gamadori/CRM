using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddSignatureOtpFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SignatureOtpHash",
                table: "TicketsInterventions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignatureOtpExpiry",
                table: "TicketsInterventions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SignatureOtpAttempts",
                table: "TicketsInterventions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SignatureOtpChallengeId",
                table: "TicketsInterventions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingSignature",
                table: "TicketsInterventions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingSignatureName",
                table: "TicketsInterventions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SignatureOtpHash",
                table: "TicketsInterventions");

            migrationBuilder.DropColumn(
                name: "SignatureOtpExpiry",
                table: "TicketsInterventions");

            migrationBuilder.DropColumn(
                name: "SignatureOtpAttempts",
                table: "TicketsInterventions");

            migrationBuilder.DropColumn(
                name: "SignatureOtpChallengeId",
                table: "TicketsInterventions");

            migrationBuilder.DropColumn(
                name: "PendingSignature",
                table: "TicketsInterventions");

            migrationBuilder.DropColumn(
                name: "PendingSignatureName",
                table: "TicketsInterventions");
        }
    }
}
