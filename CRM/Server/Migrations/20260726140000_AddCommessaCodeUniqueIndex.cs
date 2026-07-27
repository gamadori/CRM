using CRM.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Rende il codice commessa una vera chiave naturale: nvarchar(30) al posto di nvarchar(max)
    /// (che non e' indicizzabile) e indice univoco filtrato. Il filtro serve perche' SQL Server
    /// considera due NULL uguali in un indice univoco, e il codice e' nullable.
    /// I duplicati eventualmente gia' presenti (generazione read-then-write in concorrenza)
    /// vengono rinominati con suffisso -D{Id}: restano visibili e correggibili a mano.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260726140000_AddCommessaCodeUniqueIndex")]
    public partial class AddCommessaCodeUniqueIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Il restringimento del tipo non deve poter troncare dati: meglio fallire con un
            //    messaggio chiaro che perdere silenziosamente parte di un codice.
            migrationBuilder.Sql(@"
IF OBJECT_ID('Commesse') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM Commesse WHERE Code IS NOT NULL AND LEN(Code) > 30)
        RAISERROR('Migration 20260726140000: esistono codici commessa con lunghezza superiore a 30 caratteri. Accorciarli prima di applicare la migration.', 16, 1);
END
");

            // 2. Duplicati esistenti: il piu' vecchio tiene il codice, gli altri vengono marcati.
            migrationBuilder.Sql(@"
IF OBJECT_ID('Commesse') IS NOT NULL
BEGIN
    EXEC('WITH dup AS (
              SELECT Id, Code, ROW_NUMBER() OVER (PARTITION BY Code ORDER BY Id) AS rn
                FROM Commesse
               WHERE Code IS NOT NULL
          )
          UPDATE dup
             SET Code = LEFT(Code, 16) + ''-D'' + CAST(Id AS nvarchar(10))
           WHERE rn > 1');
END
");

            // 3. Tipo indicizzabile.
            migrationBuilder.Sql(@"
IF OBJECT_ID('Commesse') IS NOT NULL AND COL_LENGTH('Commesse','Code') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1
                 FROM sys.columns
                WHERE object_id = OBJECT_ID('Commesse') AND name = 'Code' AND max_length = -1)
        ALTER TABLE Commesse ALTER COLUMN Code nvarchar(30) NULL;
END
");

            // 4. Rete di sicurezza contro la race: il lock applicativo evita il conflitto,
            //    l'indice garantisce che non possa comunque passare un duplicato.
            migrationBuilder.Sql(@"
IF OBJECT_ID('Commesse') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Commesse_Code' AND object_id = OBJECT_ID('Commesse'))
BEGIN
    CREATE UNIQUE INDEX IX_Commesse_Code ON Commesse(Code) WHERE Code IS NOT NULL;
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // La rinomina dei duplicati non si annulla: i codici originali erano ambigui per definizione.
            migrationBuilder.Sql(@"
IF OBJECT_ID('Commesse') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Commesse_Code' AND object_id = OBJECT_ID('Commesse'))
    DROP INDEX IX_Commesse_Code ON Commesse;
");

            migrationBuilder.Sql(@"
IF OBJECT_ID('Commesse') IS NOT NULL AND COL_LENGTH('Commesse','Code') IS NOT NULL
    ALTER TABLE Commesse ALTER COLUMN Code nvarchar(max) NULL;
");
        }
    }
}
