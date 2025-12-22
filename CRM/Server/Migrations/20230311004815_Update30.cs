using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    public partial class Update30 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdProject",
                table: "Attachment");

            migrationBuilder.DropColumn(
                name: "IdTalk",
                table: "Attachment");

            migrationBuilder.DropColumn(
                name: "IdTicket",
                table: "Attachment");

            migrationBuilder.AddColumn<int>(
                name: "AttchmentType",
                table: "Attachment",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IdParent",
                table: "Attachment",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ContractTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Permits = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractTypes", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractTypes");

            migrationBuilder.DropColumn(
                name: "AttchmentType",
                table: "Attachment");

            migrationBuilder.DropColumn(
                name: "IdParent",
                table: "Attachment");

            migrationBuilder.AddColumn<int>(
                name: "IdProject",
                table: "Attachment",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdTalk",
                table: "Attachment",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdTicket",
                table: "Attachment",
                type: "int",
                nullable: true);
        }
    }
}
