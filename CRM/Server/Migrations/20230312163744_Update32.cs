using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    public partial class Update32 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "TicketTypes",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "ContractTypeTicketTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdContractType = table.Column<int>(type: "int", nullable: false),
                    IdTicketType = table.Column<int>(type: "int", nullable: false),
                    NumIntervention = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractTypeTicketTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractTypeTicketTypes_ContractTypes_IdContractType",
                        column: x => x.IdContractType,
                        principalTable: "ContractTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContractTypeTicketTypes_TicketTypes_IdTicketType",
                        column: x => x.IdTicketType,
                        principalTable: "TicketTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractTypeTicketTypes_IdContractType",
                table: "ContractTypeTicketTypes",
                column: "IdContractType");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTypeTicketTypes_IdTicketType",
                table: "ContractTypeTicketTypes",
                column: "IdTicketType");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractTypeTicketTypes");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "TicketTypes");
        }
    }
}
