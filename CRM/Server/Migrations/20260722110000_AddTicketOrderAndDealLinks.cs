using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260722110000_AddTicketOrderAndDealLinks")]
    public partial class AddTicketOrderAndDealLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdDeal",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdOrder",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_IdDeal",
                table: "Tickets",
                column: "IdDeal");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_IdOrder",
                table: "Tickets",
                column: "IdOrder");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Deals_IdDeal",
                table: "Tickets",
                column: "IdDeal",
                principalTable: "Deals",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Orders_IdOrder",
                table: "Tickets",
                column: "IdOrder",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Deals_IdDeal",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Orders_IdOrder",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_IdDeal",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_IdOrder",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "IdDeal",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "IdOrder",
                table: "Tickets");
        }
    }
}
