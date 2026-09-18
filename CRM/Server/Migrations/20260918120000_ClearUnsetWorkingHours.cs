using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// L'orario di lavoro non configurato torna a essere vuoto.
    /// <para>
    /// La migration Update10 (gennaio 2026) ha reso ScheduleTimeStart/End facoltativi, ma la
    /// riga esistente si e' portata dietro la mezzanotte del vecchio datetime obbligatorio:
    /// in archivio "non configurato" si scriveva 00:00-00:00. Tre lettori lo interpretavano
    /// in tre modi (preavvisi: ripiego; Timeline: nessuna fascia; salvataggio ticket: ogni
    /// orario respinto). Il codice ora passa da GlobalSetting.OrarioDiLavoro(), che tratta
    /// quel valore come vuoto; qui il dato smette di mentire.
    /// </para>
    /// <para>Solo dati, nessuna modifica di schema: lo snapshot resta invariato.</para>
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260918120000_ClearUnsetWorkingHours")]
    public partial class ClearUnsetWorkingHours : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Una mezzanotte su uno dei due estremi, o un inizio che non precede la fine:
            // l'intervallo non ha senso e viene azzerato per intero.
            migrationBuilder.Sql(@"
UPDATE GlobalSettings
SET ScheduleTimeStart = NULL, ScheduleTimeEnd = NULL
WHERE ScheduleTimeStart = '00:00'
   OR ScheduleTimeEnd = '00:00'
   OR ScheduleTimeStart >= ScheduleTimeEnd;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Il valore precedente era un residuo senza significato: non c'e' niente da ripristinare.
        }
    }
}
