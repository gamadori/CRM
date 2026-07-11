using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddInboundAiTriageAndChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalSender",
                table: "TicketChats",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AiConfidence",
                table: "InboundEmails",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AiIsSupportRequest",
                table: "InboundEmails",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiReason",
                table: "InboundEmails",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiSummary",
                table: "InboundEmails",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseAiTriage",
                table: "EmailInboxes",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalSender",
                table: "TicketChats");

            migrationBuilder.DropColumn(
                name: "AiConfidence",
                table: "InboundEmails");

            migrationBuilder.DropColumn(
                name: "AiIsSupportRequest",
                table: "InboundEmails");

            migrationBuilder.DropColumn(
                name: "AiReason",
                table: "InboundEmails");

            migrationBuilder.DropColumn(
                name: "AiSummary",
                table: "InboundEmails");

            migrationBuilder.DropColumn(
                name: "UseAiTriage",
                table: "EmailInboxes");
        }
    }
}
