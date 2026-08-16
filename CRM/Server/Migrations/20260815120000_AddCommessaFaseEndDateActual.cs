using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// La fase impara quando il lavoro e' finito davvero.
    /// <para>
    /// Finora la fase aveva solo le date pianificate: il sistema sapeva che il ticket era chiuso,
    /// non quando la lavorazione si era conclusa. Cosi' una fase chiusa in ritardo lasciava le
    /// successive alle date di partenza, e il piano continuava a dire che si finiva nei tempi.
    /// </para>
    /// <para>
    /// Le fasi gia' concluse ricevono la data dell'ultimo tempo registrato sugli interventi dei loro
    /// ticket; in mancanza, la chiusura del ticket. Le altre restano a NULL: inventare una data
    /// sarebbe peggio che non averla, perche' da qui in avanti su questo campo si sposta il piano.
    /// </para>
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260815120000_AddCommessaFaseEndDateActual")]
    public partial class AddCommessaFaseEndDateActual : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'EndDateActual' AND object_id = OBJECT_ID('dbo.CommessaFasi'))
    ALTER TABLE [dbo].[CommessaFasi] ADD [EndDateActual] datetime2 NULL;
");

            // Batch a se': la colonna appena aggiunta non e' visibile nello stesso lotto.
            // Stessa gerarchia di fonti che usa il codice: prima il lavoro registrato, poi la
            // chiusura del ticket. Le pause non contano come lavoro.
            migrationBuilder.Sql(@"
UPDATE f
   SET [EndDateActual] = COALESCE(lavoro.UltimoLavoro, chiusura.UltimaChiusura)
  FROM [dbo].[CommessaFasi] f
  OUTER APPLY (
        SELECT MAX(tt.[EndDateTime]) AS UltimoLavoro
          FROM [dbo].[Tickets] t
          JOIN [dbo].[TicketsInterventions] i ON i.[IdTicket] = t.[Id]
          JOIN [dbo].[TicketInterventionTimes] tt ON tt.[IdTicketIntervention] = i.[Id]
         WHERE t.[IdCommessaFase] = f.[Id]
           AND tt.[TimeType] <> 2
  ) lavoro
  OUTER APPLY (
        SELECT MAX(t.[DateClosed]) AS UltimaChiusura
          FROM [dbo].[Tickets] t
         WHERE t.[IdCommessaFase] = f.[Id]
           AND t.[Closed] = 1
  ) chiusura
 WHERE f.[State] = 2
   AND f.[EndDateActual] IS NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'EndDateActual' AND object_id = OBJECT_ID('dbo.CommessaFasi'))
    ALTER TABLE [dbo].[CommessaFasi] DROP COLUMN [EndDateActual];
");
        }
    }
}
