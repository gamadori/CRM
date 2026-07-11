using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketOperationalSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OperationalSummary",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OperationalSummaryUpdatedAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationalSummaryUpdatedBy",
                table: "Tickets",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_OperationalSummaryUpdatedBy",
                table: "Tickets",
                column: "OperationalSummaryUpdatedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_AspNetUsers_OperationalSummaryUpdatedBy",
                table: "Tickets",
                column: "OperationalSummaryUpdatedBy",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_AspNetUsers_OperationalSummaryUpdatedBy",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_OperationalSummaryUpdatedBy",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "OperationalSummary",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "OperationalSummaryUpdatedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "OperationalSummaryUpdatedBy",
                table: "Tickets");
        }
    }
}
