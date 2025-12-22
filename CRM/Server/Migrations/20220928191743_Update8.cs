using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    public partial class Update8 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketTypesLanguages_Languages_LanguageId",
                table: "TicketTypesLanguages");

            migrationBuilder.DropIndex(
                name: "IX_TicketTypesLanguages_LanguageId",
                table: "TicketTypesLanguages");

            migrationBuilder.DropColumn(
                name: "LanguageId",
                table: "TicketTypesLanguages");

            migrationBuilder.CreateIndex(
                name: "IX_TicketTypesLanguages_IdLanguage",
                table: "TicketTypesLanguages",
                column: "IdLanguage");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketTypesLanguages_Languages_IdLanguage",
                table: "TicketTypesLanguages",
                column: "IdLanguage",
                principalTable: "Languages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketTypesLanguages_Languages_IdLanguage",
                table: "TicketTypesLanguages");

            migrationBuilder.DropIndex(
                name: "IX_TicketTypesLanguages_IdLanguage",
                table: "TicketTypesLanguages");

            migrationBuilder.AddColumn<int>(
                name: "LanguageId",
                table: "TicketTypesLanguages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketTypesLanguages_LanguageId",
                table: "TicketTypesLanguages",
                column: "LanguageId");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketTypesLanguages_Languages_LanguageId",
                table: "TicketTypesLanguages",
                column: "LanguageId",
                principalTable: "Languages",
                principalColumn: "Id");
        }
    }
}
