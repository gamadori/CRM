using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260731140000_ExpenseReceiptDocumentCurrencyAndPages")]
    public partial class ExpenseReceiptDocumentCurrencyAndPages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmountBase",
                table: "ExpenseReceiptDocuments",
                type: "decimal(18,2)",
                nullable: true);
            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "ExpenseReceiptDocuments",
                type: "decimal(18,6)",
                nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "PageFrom",
                table: "ExpenseReceiptDocuments",
                type: "int",
                nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "PageTo",
                table: "ExpenseReceiptDocuments",
                type: "int",
                nullable: true);
            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmountBase",
                table: "ExpenseReceiptDocuments",
                type: "decimal(18,2)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AmountBase", table: "ExpenseReceiptDocuments");
            migrationBuilder.DropColumn(name: "ExchangeRate", table: "ExpenseReceiptDocuments");
            migrationBuilder.DropColumn(name: "PageFrom", table: "ExpenseReceiptDocuments");
            migrationBuilder.DropColumn(name: "PageTo", table: "ExpenseReceiptDocuments");
            migrationBuilder.DropColumn(name: "TaxAmountBase", table: "ExpenseReceiptDocuments");
        }
    }
}
