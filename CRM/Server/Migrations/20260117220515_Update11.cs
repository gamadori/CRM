using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class Update11 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtractedCurrency",
                table: "TicketsInterventions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedDescription",
                table: "TicketsInterventions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedFieldsJson",
                table: "TicketsInterventions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedMerchantName",
                table: "TicketsInterventions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExtractedTaxAmount",
                table: "TicketsInterventions",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExtractedTotalAmount",
                table: "TicketsInterventions",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExtractedTransactionDate",
                table: "TicketsInterventions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "ExtractionConfidence",
                table: "TicketsInterventions",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExtractionConfirmed",
                table: "TicketsInterventions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReceiptAttachmentFileId",
                table: "TicketsInterventions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceiptProcessedDate",
                table: "TicketsInterventions",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtractedCurrency",
                table: "TicketsInterventions");

            migrationBuilder.DropColumn(
                name: "ExtractedDescription",
                table: "TicketsInterventions");

            migrationBuilder.DropColumn(
                name: "ExtractedFieldsJson",
                table: "TicketsInterventions");

            migrationBuilder.DropColumn(
                name: "ExtractedMerchantName",
                table: "TicketsInterventions");

            migrationBuilder.DropColumn(
                name: "ExtractedTaxAmount",
                table: "TicketsInterventions");

            migrationBuilder.DropColumn(
                name: "ExtractedTotalAmount",
                table: "TicketsInterventions");

            migrationBuilder.DropColumn(
                name: "ExtractedTransactionDate",
                table: "TicketsInterventions");

            migrationBuilder.DropColumn(
                name: "ExtractionConfidence",
                table: "TicketsInterventions");

            migrationBuilder.DropColumn(
                name: "ExtractionConfirmed",
                table: "TicketsInterventions");

            migrationBuilder.DropColumn(
                name: "ReceiptAttachmentFileId",
                table: "TicketsInterventions");

            migrationBuilder.DropColumn(
                name: "ReceiptProcessedDate",
                table: "TicketsInterventions");
        }
    }
}
