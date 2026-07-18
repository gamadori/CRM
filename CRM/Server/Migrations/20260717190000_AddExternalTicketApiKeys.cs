using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    public partial class AddExternalTicketApiKeys : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalTicketApiKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    KeyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    KeyPrefix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IdCompany = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalTicketApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalTicketApiKeys_Companies_IdCompany",
                        column: x => x.IdCompany,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTicketApiKeys_IdCompany",
                table: "ExternalTicketApiKeys",
                column: "IdCompany");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTicketApiKeys_KeyHash",
                table: "ExternalTicketApiKeys",
                column: "KeyHash",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalTicketApiKeys");
        }
    }
}
