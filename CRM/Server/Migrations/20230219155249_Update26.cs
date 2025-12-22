using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    public partial class Update26 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Accessories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Date",
                table: "Accessories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "IdUser",
                table: "Accessories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierCode",
                table: "Accessories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccessoryTypeLanguages_IdAccessoryType",
                table: "AccessoryTypeLanguages",
                column: "IdAccessoryType");

            migrationBuilder.CreateIndex(
                name: "IX_Accessories_IdAccessoryType",
                table: "Accessories",
                column: "IdAccessoryType");

            migrationBuilder.AddForeignKey(
                name: "FK_Accessories_AccessoryTypes_IdAccessoryType",
                table: "Accessories",
                column: "IdAccessoryType",
                principalTable: "AccessoryTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AccessoryTypeLanguages_AccessoryTypes_IdAccessoryType",
                table: "AccessoryTypeLanguages",
                column: "IdAccessoryType",
                principalTable: "AccessoryTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accessories_AccessoryTypes_IdAccessoryType",
                table: "Accessories");

            migrationBuilder.DropForeignKey(
                name: "FK_AccessoryTypeLanguages_AccessoryTypes_IdAccessoryType",
                table: "AccessoryTypeLanguages");

            migrationBuilder.DropIndex(
                name: "IX_AccessoryTypeLanguages_IdAccessoryType",
                table: "AccessoryTypeLanguages");

            migrationBuilder.DropIndex(
                name: "IX_Accessories_IdAccessoryType",
                table: "Accessories");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Accessories");

            migrationBuilder.DropColumn(
                name: "Date",
                table: "Accessories");

            migrationBuilder.DropColumn(
                name: "IdUser",
                table: "Accessories");

            migrationBuilder.DropColumn(
                name: "SupplierCode",
                table: "Accessories");
        }
    }
}
