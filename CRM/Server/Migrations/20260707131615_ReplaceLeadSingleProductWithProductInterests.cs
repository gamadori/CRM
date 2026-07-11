using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceLeadSingleProductWithProductInterests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Products_IdProduct",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_IdProduct",
                table: "Leads");

            migrationBuilder.CreateTable(
                name: "DealProductInterests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdDeal = table.Column<int>(type: "int", nullable: false),
                    IdProduct = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "Money", nullable: false),
                    DiscountPct = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "Money", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealProductInterests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DealProductInterests_Deals_IdDeal",
                        column: x => x.IdDeal,
                        principalTable: "Deals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DealProductInterests_Products_IdProduct",
                        column: x => x.IdProduct,
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LeadProductInterests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdLead = table.Column<int>(type: "int", nullable: false),
                    IdProduct = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "Money", nullable: false),
                    DiscountPct = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "Money", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadProductInterests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadProductInterests_Leads_IdLead",
                        column: x => x.IdLead,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeadProductInterests_Products_IdProduct",
                        column: x => x.IdProduct,
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DealProductInterests_IdDeal",
                table: "DealProductInterests",
                column: "IdDeal");

            migrationBuilder.CreateIndex(
                name: "IX_DealProductInterests_IdProduct",
                table: "DealProductInterests",
                column: "IdProduct");

            migrationBuilder.CreateIndex(
                name: "IX_LeadProductInterests_IdLead",
                table: "LeadProductInterests",
                column: "IdLead");

            migrationBuilder.CreateIndex(
                name: "IX_LeadProductInterests_IdProduct",
                table: "LeadProductInterests",
                column: "IdProduct");

            migrationBuilder.Sql("""
                INSERT INTO LeadProductInterests (IdLead, IdProduct, Quantity, UnitPrice, DiscountPct, LineTotal, SortOrder)
                SELECT
                    l.Id,
                    l.IdProduct,
                    1,
                    CASE WHEN l.EstimatedValue > 0 THEN l.EstimatedValue ELSE ISNULL(p.Price, 0) END,
                    0,
                    CASE WHEN l.EstimatedValue > 0 THEN l.EstimatedValue ELSE ISNULL(p.Price, 0) END,
                    0
                FROM Leads l
                LEFT JOIN Products p ON p.Id = l.IdProduct
                WHERE l.IdProduct IS NOT NULL
                """);

            migrationBuilder.DropColumn(
                name: "IdProduct",
                table: "Leads");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdProduct",
                table: "Leads",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE l
                SET IdProduct = src.IdProduct
                FROM Leads l
                INNER JOIN (
                    SELECT IdLead, MIN(IdProduct) AS IdProduct
                    FROM LeadProductInterests
                    GROUP BY IdLead
                ) src ON src.IdLead = l.Id
                """);

            migrationBuilder.DropTable(
                name: "DealProductInterests");

            migrationBuilder.DropTable(
                name: "LeadProductInterests");

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
    }
}
