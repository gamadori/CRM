using CRM.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Le note spese smettono di essere un'appendice dell'intervento.
    /// <para>
    /// Nel mondo reale una spesa ha SEMPRE una persona e una data, e solo A VOLTE un lavoro a cui
    /// riferirsi: trasferta pre-vendita, visita di cortesia, corso, bolletta non hanno nessun
    /// intervento. Il modello aveva l'esatto contrario — intervento obbligatorio, chi ha speso
    /// assente — e chi doveva registrare quei casi era costretto ad appenderli a un intervento a
    /// caso, sporcando proprio il costo del lavoro per cui il collegamento esisteva.
    /// </para>
    /// <para>
    /// Sul cambio: viene memorizzato insieme all'importo convertito e non ricalcolato in lettura.
    /// Un totale che si muove con il cambio del giorno non e' utilizzabile per un rimborso, e il
    /// totale del mese scorso deve restare quello di allora.
    /// </para>
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260731090000_ExpenseReceiptsSpineAndCurrency")]
    public partial class ExpenseReceiptsSpineAndCurrency : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Valuta base dell'installazione ──────────────────────────────────────────
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'BaseCurrency' AND object_id = OBJECT_ID('GlobalSettings'))
    ALTER TABLE GlobalSettings ADD BaseCurrency nvarchar(3) NOT NULL
        CONSTRAINT DF_GlobalSettings_BaseCurrency DEFAULT 'EUR';
");

            // ── Chi ha speso: la spina dorsale che mancava ──────────────────────────────
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'IdUserSpender' AND object_id = OBJECT_ID('ExpenseReceipts'))
    ALTER TABLE ExpenseReceipts ADD IdUserSpender nvarchar(450) NULL;
");

            // ── Contesto alternativo all'intervento (visite commerciali e di cortesia) ──
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'IdActivity' AND object_id = OBJECT_ID('ExpenseReceipts'))
    ALTER TABLE ExpenseReceipts ADD IdActivity int NULL;
");

            // ── Cambio congelato + importo convertito ───────────────────────────────────
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'ExchangeRate' AND object_id = OBJECT_ID('ExpenseReceipts'))
    ALTER TABLE ExpenseReceipts ADD ExchangeRate decimal(18,6) NULL;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'AmountBase' AND object_id = OBJECT_ID('ExpenseReceipts'))
    ALTER TABLE ExpenseReceipts ADD AmountBase decimal(18,2) NULL;
");

            // ── L'intervento diventa facoltativo ────────────────────────────────────────
            // La FK va rimossa prima di poter cambiare la nullabilita' della colonna, e il suo
            // nome e' generato: si ricava da sys.foreign_keys invece di scriverlo a mano.
            migrationBuilder.Sql(@"
DECLARE @fk sysname = (
    SELECT TOP 1 fk.name
    FROM sys.foreign_keys fk
    JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
    JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
    WHERE fk.parent_object_id = OBJECT_ID('ExpenseReceipts') AND c.name = 'TicketInterventionId');

IF @fk IS NOT NULL
    EXEC('ALTER TABLE ExpenseReceipts DROP CONSTRAINT [' + @fk + ']');
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'TicketInterventionId'
           AND object_id = OBJECT_ID('ExpenseReceipts') AND is_nullable = 0)
    ALTER TABLE ExpenseReceipts ALTER COLUMN TicketInterventionId int NULL;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ExpenseReceipts_TicketsInterventions_TicketInterventionId')
    ALTER TABLE ExpenseReceipts ADD CONSTRAINT FK_ExpenseReceipts_TicketsInterventions_TicketInterventionId
        FOREIGN KEY (TicketInterventionId) REFERENCES TicketsInterventions (Id);
");

            // NO ACTION (il default) e non SET NULL su entrambe: SQL Server rifiuta il SET NULL
            // perche' ExpenseReceipts sarebbe raggiungibile per piu' percorsi di propagazione.
            // La semantica che ne esce e' anche piu' sana: non si cancella un'attivita' o un utente
            // lasciando in giro spese orfane del loro contesto, si sistemano prima le spese.
            // Gli utenti sono comunque soft-deleted (IsDeleted), quindi il caso non si presenta.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ExpenseReceipts_Activities_IdActivity')
    ALTER TABLE ExpenseReceipts ADD CONSTRAINT FK_ExpenseReceipts_Activities_IdActivity
        FOREIGN KEY (IdActivity) REFERENCES Activities (Id);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ExpenseReceipts_AspNetUsers_IdUserSpender')
    ALTER TABLE ExpenseReceipts ADD CONSTRAINT FK_ExpenseReceipts_AspNetUsers_IdUserSpender
        FOREIGN KEY (IdUserSpender) REFERENCES AspNetUsers (Id);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExpenseReceipts_IdActivity' AND object_id = OBJECT_ID('ExpenseReceipts'))
    CREATE INDEX IX_ExpenseReceipts_IdActivity ON ExpenseReceipts (IdActivity);
");

            // I totali si leggono quasi sempre per persona e periodo: e' l'indice della pagina.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExpenseReceipts_IdUserSpender' AND object_id = OBJECT_ID('ExpenseReceipts'))
    CREATE INDEX IX_ExpenseReceipts_IdUserSpender ON ExpenseReceipts (IdUserSpender)
        INCLUDE (TransactionDate, AmountBase, Currency);
");

            // ── Backfill ───────────────────────────────────────────────────────────────
            // Chi ha speso: si ricava dal primo utente assegnato all'intervento, con ripiego
            // sull'assegnatario del ticket. E' un'euristica dichiarata, non un dato certo: le
            // righe esistenti sono poche e vanno riviste a occhio.
            migrationBuilder.Sql(@"
UPDATE er
SET IdUserSpender = COALESCE(
    (SELECT TOP 1 tiu.IdUser FROM TicketInterventionUser tiu
     WHERE tiu.IdIntervention = er.TicketInterventionId ORDER BY tiu.Id),
    (SELECT TOP 1 t.IdUserAssigned FROM TicketsInterventions ti
     JOIN Tickets t ON t.Id = ti.IdTicket WHERE ti.Id = er.TicketInterventionId))
FROM ExpenseReceipts er
WHERE er.IdUserSpender IS NULL AND er.TicketInterventionId IS NOT NULL;
");

            // Le spese gia' nella valuta base sono convertite per definizione: cambio 1.
            // Le altre restano da convertire, e la pagina le mostrera' come tali.
            migrationBuilder.Sql(@"
UPDATE ExpenseReceipts
SET ExchangeRate = 1, AmountBase = TotalAmount
WHERE AmountBase IS NULL AND TotalAmount IS NOT NULL
  AND Currency = (SELECT TOP 1 BaseCurrency FROM GlobalSettings ORDER BY Id);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ExpenseReceipts_AspNetUsers_IdUserSpender')
    ALTER TABLE ExpenseReceipts DROP CONSTRAINT FK_ExpenseReceipts_AspNetUsers_IdUserSpender;
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ExpenseReceipts_Activities_IdActivity')
    ALTER TABLE ExpenseReceipts DROP CONSTRAINT FK_ExpenseReceipts_Activities_IdActivity;
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExpenseReceipts_IdUserSpender' AND object_id = OBJECT_ID('ExpenseReceipts'))
    DROP INDEX IX_ExpenseReceipts_IdUserSpender ON ExpenseReceipts;
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExpenseReceipts_IdActivity' AND object_id = OBJECT_ID('ExpenseReceipts'))
    DROP INDEX IX_ExpenseReceipts_IdActivity ON ExpenseReceipts;
");

            // Si torna a intervento obbligatorio solo se nessuna riga ne e' priva: altrimenti la
            // ALTER fallirebbe, ed e' giusto che fallisca invece di cancellare quelle righe.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ExpenseReceipts_TicketsInterventions_TicketInterventionId')
    ALTER TABLE ExpenseReceipts DROP CONSTRAINT FK_ExpenseReceipts_TicketsInterventions_TicketInterventionId;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM ExpenseReceipts WHERE TicketInterventionId IS NULL)
BEGIN
    ALTER TABLE ExpenseReceipts ALTER COLUMN TicketInterventionId int NOT NULL;
    ALTER TABLE ExpenseReceipts ADD CONSTRAINT FK_ExpenseReceipts_TicketsInterventions_TicketInterventionId
        FOREIGN KEY (TicketInterventionId) REFERENCES TicketsInterventions (Id);
END
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'AmountBase' AND object_id = OBJECT_ID('ExpenseReceipts'))
    ALTER TABLE ExpenseReceipts DROP COLUMN AmountBase;
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'ExchangeRate' AND object_id = OBJECT_ID('ExpenseReceipts'))
    ALTER TABLE ExpenseReceipts DROP COLUMN ExchangeRate;
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'IdActivity' AND object_id = OBJECT_ID('ExpenseReceipts'))
    ALTER TABLE ExpenseReceipts DROP COLUMN IdActivity;
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'IdUserSpender' AND object_id = OBJECT_ID('ExpenseReceipts'))
    ALTER TABLE ExpenseReceipts DROP COLUMN IdUserSpender;
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_GlobalSettings_BaseCurrency')
    ALTER TABLE GlobalSettings DROP CONSTRAINT DF_GlobalSettings_BaseCurrency;
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'BaseCurrency' AND object_id = OBJECT_ID('GlobalSettings'))
    ALTER TABLE GlobalSettings DROP COLUMN BaseCurrency;
");
        }
    }
}
