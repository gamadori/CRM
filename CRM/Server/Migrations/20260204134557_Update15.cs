using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class Update15 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketInterventionUser_AspNetUsers_IdUser",
                table: "TicketInterventionUser");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketInterventionUser_AspNetUsers_IdUser",
                table: "TicketInterventionUser",
                column: "IdUser",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketInterventionUser_AspNetUsers_IdUser",
                table: "TicketInterventionUser");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketInterventionUser_AspNetUsers_IdUser",
                table: "TicketInterventionUser",
                column: "IdUser",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
