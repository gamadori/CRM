using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class Update24 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseReceipts_TicketsInterventions_TicketInterventionId",
                table: "ExpenseReceipts");

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseReceipts_TicketsInterventions_TicketInterventionId",
                table: "ExpenseReceipts",
                column: "TicketInterventionId",
                principalTable: "TicketsInterventions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseReceipts_TicketsInterventions_TicketInterventionId",
                table: "ExpenseReceipts");

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseReceipts_TicketsInterventions_TicketInterventionId",
                table: "ExpenseReceipts",
                column: "TicketInterventionId",
                principalTable: "TicketsInterventions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
