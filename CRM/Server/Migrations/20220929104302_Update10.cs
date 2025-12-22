using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    public partial class Update10 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.CreateIndex(
                name: "IX_TicketTypesLanguages_IdTicketType",
                table: "TicketTypesLanguages",
                column: "IdTicketType");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketTypesLanguages_TicketTypes_IdTicketType",
                table: "TicketTypesLanguages",
                column: "IdTicketType",
                principalTable: "TicketTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketTypesLanguages_TicketTypes_IdTicketType",
                table: "TicketTypesLanguages");

            migrationBuilder.DropIndex(
                name: "IX_TicketTypesLanguages_IdTicketType",
                table: "TicketTypesLanguages");

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
    }
}
