using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketChatAttachmentFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdAttachmentFile",
                table: "TicketChats",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketChats_IdAttachmentFile",
                table: "TicketChats",
                column: "IdAttachmentFile");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketChats_AttachmentFiles_IdAttachmentFile",
                table: "TicketChats",
                column: "IdAttachmentFile",
                principalTable: "AttachmentFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketChats_AttachmentFiles_IdAttachmentFile",
                table: "TicketChats");

            migrationBuilder.DropIndex(
                name: "IX_TicketChats_IdAttachmentFile",
                table: "TicketChats");

            migrationBuilder.DropColumn(
                name: "IdAttachmentFile",
                table: "TicketChats");
        }
    }
}
