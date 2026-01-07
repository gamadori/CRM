using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddSignatureEmailConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SignatureEmail",
                table: "TicketsInterventions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SignatureStatus",
                table: "TicketsInterventions",
                type: "int",
                nullable: false,
                defaultValue: 0); // Pending

            migrationBuilder.AddColumn<string>(
                name: "SignatureConfirmationToken",
                table: "TicketsInterventions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignatureConfirmedDate",
                table: "TicketsInterventions",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SignatureEmail",
                table: "TicketsInterventions");

            migrationBuilder.DropColumn(
                name: "SignatureStatus",
                table: "TicketsInterventions");

            migrationBuilder.DropColumn(
                name: "SignatureConfirmationToken",
                table: "TicketsInterventions");

            migrationBuilder.DropColumn(
                name: "SignatureConfirmedDate",
                table: "TicketsInterventions");
        }
    }
}
