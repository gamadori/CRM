using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    public partial class Update9 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TicketTypeId",
                table: "TicketTypesLanguages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketTypesLanguages_TicketTypeId",
                table: "TicketTypesLanguages",
                column: "TicketTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketTypesLanguages_TicketTypes_TicketTypeId",
                table: "TicketTypesLanguages",
                column: "TicketTypeId",
                principalTable: "TicketTypes",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketTypesLanguages_TicketTypes_TicketTypeId",
                table: "TicketTypesLanguages");

            migrationBuilder.DropIndex(
                name: "IX_TicketTypesLanguages_TicketTypeId",
                table: "TicketTypesLanguages");

            migrationBuilder.DropColumn(
                name: "TicketTypeId",
                table: "TicketTypesLanguages");
        }
    }
}
