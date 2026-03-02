using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class Update23 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdCompany",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_IdCompany",
                table: "Products",
                column: "IdCompany");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Companies_IdCompany",
                table: "Products",
                column: "IdCompany",
                principalTable: "Companies",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Companies_IdCompany",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_IdCompany",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IdCompany",
                table: "Products");
        }
    }
}
