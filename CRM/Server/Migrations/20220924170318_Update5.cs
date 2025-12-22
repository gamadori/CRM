using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    public partial class Update5 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdParent",
                table: "Attachment");

            migrationBuilder.AddColumn<int>(
                name: "IdProject",
                table: "Attachment",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdTicket",
                table: "Attachment",
                type: "int",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdProject",
                table: "Attachment");

            migrationBuilder.DropColumn(
                name: "IdTicket",
                table: "Attachment");

            migrationBuilder.AddColumn<int>(
                name: "IdParent",
                table: "Attachment",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
