using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Commesse aperte (senza template) e baseline del consuntivo.
    /// Kind distingue una commessa nata vuota per scelta da una vuota per errore: le guardie
    /// sull'avvio produzione continuano a rifiutare la seconda.
    /// EndDateBaseline e BudgetHoursBaseline conservano la promessa fatta all'avvio, che
    /// EndDatePlanned e BudgetHours non conservano perché si muovono a ogni riprogrammazione.
    /// Le commesse già esistenti restano senza baseline: la loro promessa iniziale non è più
    /// ricostruibile e inventarla dalla consegna attuale renderebbe puntuali anche quelle slittate.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260803120000_AddCommessaKindAndBaseline")]
    public partial class AddCommessaKindAndBaseline : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('Commesse') IS NOT NULL AND COL_LENGTH('Commesse','Kind') IS NULL
    ALTER TABLE Commesse ADD Kind int NOT NULL CONSTRAINT DF_Commesse_Kind DEFAULT(0);

IF OBJECT_ID('Commesse') IS NOT NULL AND COL_LENGTH('Commesse','EndDateBaseline') IS NULL
    ALTER TABLE Commesse ADD EndDateBaseline datetime2 NULL;

IF OBJECT_ID('Commesse') IS NOT NULL AND COL_LENGTH('Commesse','BudgetHoursBaseline') IS NULL
    ALTER TABLE Commesse ADD BudgetHoursBaseline int NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('Commesse') IS NOT NULL AND COL_LENGTH('Commesse','BudgetHoursBaseline') IS NOT NULL
    ALTER TABLE Commesse DROP COLUMN BudgetHoursBaseline;

IF OBJECT_ID('Commesse') IS NOT NULL AND COL_LENGTH('Commesse','EndDateBaseline') IS NOT NULL
    ALTER TABLE Commesse DROP COLUMN EndDateBaseline;

IF OBJECT_ID('Commesse') IS NOT NULL AND COL_LENGTH('Commesse','Kind') IS NOT NULL
BEGIN
    DECLARE @dfKind sysname;
    SELECT @dfKind = dc.name
      FROM sys.default_constraints dc
      JOIN sys.columns c ON c.default_object_id = dc.object_id
     WHERE dc.parent_object_id = OBJECT_ID('Commesse') AND c.name = 'Kind';
    IF @dfKind IS NOT NULL EXEC('ALTER TABLE Commesse DROP CONSTRAINT ' + @dfKind);
    ALTER TABLE Commesse DROP COLUMN Kind;
END
");
        }
    }
}
