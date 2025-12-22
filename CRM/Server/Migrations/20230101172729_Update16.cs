using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    public partial class Update16 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketChatReads_TicketChats_TicketChatId",
                table: "TicketChatReads");

            migrationBuilder.DropIndex(
                name: "IX_TicketChatReads_TicketChatId",
                table: "TicketChatReads");

            migrationBuilder.DropColumn(
                name: "TicketChatId",
                table: "TicketChatReads");

            migrationBuilder.CreateIndex(
                name: "IX_TicketChatReads_IdTicketChat",
                table: "TicketChatReads",
                column: "IdTicketChat");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketChatReads_TicketChats_IdTicketChat",
                table: "TicketChatReads",
                column: "IdTicketChat",
                principalTable: "TicketChats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketChatReads_TicketChats_IdTicketChat",
                table: "TicketChatReads");

            migrationBuilder.DropIndex(
                name: "IX_TicketChatReads_IdTicketChat",
                table: "TicketChatReads");

            migrationBuilder.AddColumn<int>(
                name: "TicketChatId",
                table: "TicketChatReads",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketChatReads_TicketChatId",
                table: "TicketChatReads",
                column: "TicketChatId");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketChatReads_TicketChats_TicketChatId",
                table: "TicketChatReads",
                column: "TicketChatId",
                principalTable: "TicketChats",
                principalColumn: "Id");
        }
    }
}
