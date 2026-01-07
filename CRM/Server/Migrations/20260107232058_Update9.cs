using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class Update9 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SignatureConfirmationToken",
                table: "TicketsInterventions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignatureConfirmedDate",
                table: "TicketsInterventions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureEmail",
                table: "TicketsInterventions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SignatureStatus",
                table: "TicketsInterventions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SignatureConfirmationToken",
                table: "TicketsInterventions");

            migrationBuilder.DropColumn(
                name: "SignatureConfirmedDate",
                table: "TicketsInterventions");

            migrationBuilder.DropColumn(
                name: "SignatureEmail",
                table: "TicketsInterventions");

            migrationBuilder.DropColumn(
                name: "SignatureStatus",
                table: "TicketsInterventions");
        }
    }
}
