using System;
using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Registro dei consumi dei servizi esterni a pagamento (Claude, Azure).
    /// <para>
    /// Nasce vuoto e non si popola a ritroso: prima di oggi il dato non e' mai stato raccolto, e
    /// non esiste da nessuna parte da cui recuperarlo. I totali partono da qui.
    /// </para>
    /// <para>
    /// Gli indici sono quelli delle due domande che la tabella deve reggere: "quanto nel periodo"
    /// (OccurredAt) e "quanto per funzione nel periodo" (Feature + OccurredAt). Non se ne aggiungono
    /// altri: e' una tabella che cresce a ogni chiamata AI, e ogni indice si paga in scrittura.
    /// </para>
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260808120000_AddExternalServiceUsage")]
    public partial class AddExternalServiceUsage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalServiceUsages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    Feature = table.Column<int>(type: "int", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IdUser = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    CacheReadTokens = table.Column<long>(type: "bigint", nullable: false),
                    CacheWriteTokens = table.Column<long>(type: "bigint", nullable: false),
                    Units = table.Column<int>(type: "int", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    DurationMs = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalServiceUsages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalServiceUsages_OccurredAt",
                table: "ExternalServiceUsages",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalServiceUsages_Feature_OccurredAt",
                table: "ExternalServiceUsages",
                columns: new[] { "Feature", "OccurredAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ExternalServiceUsages");
        }
    }
}
