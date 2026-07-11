using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadInterestedProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdProduct",
                table: "Leads",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leads_IdProduct",
                table: "Leads",
                column: "IdProduct");

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Products_IdProduct",
                table: "Leads",
                column: "IdProduct",
                principalTable: "Products",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Products_IdProduct",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_IdProduct",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "IdProduct",
                table: "Leads");
        }
    }
}
