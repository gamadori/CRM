using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddInboundTicketing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdTicket",
                table: "InboundEmails",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdDefaultOwner",
                table: "EmailInboxes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdDefaultType",
                table: "EmailInboxes",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdTicket",
                table: "InboundEmails");

            migrationBuilder.DropColumn(
                name: "IdDefaultOwner",
                table: "EmailInboxes");

            migrationBuilder.DropColumn(
                name: "IdDefaultType",
                table: "EmailInboxes");
        }
    }
}
