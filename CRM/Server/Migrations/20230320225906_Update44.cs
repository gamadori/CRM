using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class Update44 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_CompanyContracts_CompanyContractId",
                table: "Tickets");

            migrationBuilder.RenameColumn(
                name: "CompanyContractId",
                table: "Tickets",
                newName: "IdContact");

            migrationBuilder.RenameIndex(
                name: "IX_Tickets_CompanyContractId",
                table: "Tickets",
                newName: "IX_Tickets_IdContact");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Contacts_IdContact",
                table: "Tickets",
                column: "IdContact",
                principalTable: "Contacts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Contacts_IdContact",
                table: "Tickets");

            migrationBuilder.RenameColumn(
                name: "IdContact",
                table: "Tickets",
                newName: "CompanyContractId");

            migrationBuilder.RenameIndex(
                name: "IX_Tickets_IdContact",
                table: "Tickets",
                newName: "IX_Tickets_CompanyContractId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_CompanyContracts_CompanyContractId",
                table: "Tickets",
                column: "CompanyContractId",
                principalTable: "CompanyContracts",
                principalColumn: "Id");
        }
    }
}
