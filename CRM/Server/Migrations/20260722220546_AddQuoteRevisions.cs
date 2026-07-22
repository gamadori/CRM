using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdRootQuote",
                table: "Quotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCurrent",
                table: "Quotes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "Quotes",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "SupersededAt",
                table: "Quotes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_IdRootQuote",
                table: "Quotes",
                column: "IdRootQuote");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_IsCurrent",
                table: "Quotes",
                column: "IsCurrent");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_Quotes_IdRootQuote",
                table: "Quotes",
                column: "IdRootQuote",
                principalTable: "Quotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quotes_Quotes_IdRootQuote",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_IdRootQuote",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_IsCurrent",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "IdRootQuote",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "IsCurrent",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "SupersededAt",
                table: "Quotes");
        }
    }
}
