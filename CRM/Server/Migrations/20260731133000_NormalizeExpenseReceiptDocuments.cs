using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260731133000_NormalizeExpenseReceiptDocuments")]
    public partial class NormalizeExpenseReceiptDocuments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpenseReceiptDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpenseReceiptId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MerchantName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SubtotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ExtractionConfidence = table.Column<float>(type: "real", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseReceiptDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseReceiptDocuments_ExpenseReceipts_ExpenseReceiptId",
                        column: x => x.ExpenseReceiptId,
                        principalTable: "ExpenseReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseReceiptDocumentLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpenseReceiptDocumentId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ExtractionConfidence = table.Column<float>(type: "real", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseReceiptDocumentLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseReceiptDocumentLines_ExpenseReceiptDocuments_ExpenseReceiptDocumentId",
                        column: x => x.ExpenseReceiptDocumentId,
                        principalTable: "ExpenseReceiptDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseReceiptDocuments_ExpenseReceiptId",
                table: "ExpenseReceiptDocuments",
                column: "ExpenseReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseReceiptDocumentLines_ExpenseReceiptDocumentId",
                table: "ExpenseReceiptDocumentLines",
                column: "ExpenseReceiptDocumentId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ExpenseReceiptDocumentLines");
            migrationBuilder.DropTable(name: "ExpenseReceiptDocuments");
        }
    }
}
