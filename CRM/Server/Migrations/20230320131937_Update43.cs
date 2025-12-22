using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class Update43 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Permits",
                table: "ContractTypes");

            migrationBuilder.RenameColumn(
                name: "IdContract",
                table: "Tickets",
                newName: "CompanyContractId");

            migrationBuilder.AddColumn<bool>(
                name: "Enabled",
                table: "CompanyContracts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CompanyContractId",
                table: "Tickets",
                column: "CompanyContractId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_CompanyContracts_CompanyContractId",
                table: "Tickets",
                column: "CompanyContractId",
                principalTable: "CompanyContracts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_CompanyContracts_CompanyContractId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_CompanyContractId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Enabled",
                table: "CompanyContracts");

            migrationBuilder.RenameColumn(
                name: "CompanyContractId",
                table: "Tickets",
                newName: "IdContract");

            migrationBuilder.AddColumn<int>(
                name: "Permits",
                table: "ContractTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
