using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Tipologia della nota spese (vitto, alloggio, trasporti...) con la sua provenienza.
    /// <para>
    /// Tutte le colonne sono nullable e senza valore di ripiego: le spese gia' registrate NON
    /// vengono classificate a posteriori. Riempirle con "Altro" farebbe sembrare fatto un lavoro
    /// che nessuno ha fatto, e in un rimborso quella e' la voce da cui dipende la deducibilita'.
    /// Restano "da indicare", e il filtro apposta nell'elenco serve a smaltirle.
    /// </para>
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260807120000_AddExpenseCategory")]
    public partial class AddExpenseCategory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "ExpenseReceipts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CategorySuggested",
                table: "ExpenseReceipts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CategorySource",
                table: "ExpenseReceipts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CategoryConfidence",
                table: "ExpenseReceipts",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategoryReason",
                table: "ExpenseReceipts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "ExpenseReceiptDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CategorySource",
                table: "ExpenseReceiptDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CategoryConfidence",
                table: "ExpenseReceiptDocuments",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategoryReason",
                table: "ExpenseReceiptDocuments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // Il ripiego sul modello nasce spento: i due livelli deterministici non costano
            // niente, questo si paga a chiamata e va acceso da chi lo paga.
            migrationBuilder.AddColumn<bool>(
                name: "ExpenseCategoryAiEnabled",
                table: "GlobalSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "ExpenseCategoryMinConfidence",
                table: "GlobalSettings",
                type: "float",
                nullable: false,
                defaultValue: 0.6);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Category", table: "ExpenseReceipts");
            migrationBuilder.DropColumn(name: "CategorySuggested", table: "ExpenseReceipts");
            migrationBuilder.DropColumn(name: "CategorySource", table: "ExpenseReceipts");
            migrationBuilder.DropColumn(name: "CategoryConfidence", table: "ExpenseReceipts");
            migrationBuilder.DropColumn(name: "CategoryReason", table: "ExpenseReceipts");

            migrationBuilder.DropColumn(name: "Category", table: "ExpenseReceiptDocuments");
            migrationBuilder.DropColumn(name: "CategorySource", table: "ExpenseReceiptDocuments");
            migrationBuilder.DropColumn(name: "CategoryConfidence", table: "ExpenseReceiptDocuments");
            migrationBuilder.DropColumn(name: "CategoryReason", table: "ExpenseReceiptDocuments");

            migrationBuilder.DropColumn(name: "ExpenseCategoryAiEnabled", table: "GlobalSettings");
            migrationBuilder.DropColumn(name: "ExpenseCategoryMinConfidence", table: "GlobalSettings");
        }
    }
}
