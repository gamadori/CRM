using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Toglie di mezzo il ruolo "responsabile" fra i membri: il responsabile e'
    /// <c>Initiatives.IdOwner</c> e sta in un posto solo.
    /// <para>
    /// Il valore 0 non esiste piu' nell'enum. Le righe che lo avessero - nessuna in un database
    /// nato dalla migration precedente, che scriveva 1, ma non si puo' escludere una scrittura
    /// diretta - diventerebbero un ruolo senza nome, che l'interfaccia mostrerebbe come
    /// "Partecipante" senza che il dato lo sia. Meglio normalizzarle davvero.
    /// </para>
    /// <para>
    /// Scritta a mano perche' su questo SDK "dotnet ef migrations add" non funziona; istruzione
    /// idempotente per costruzione (rieseguirla non trova piu' righe da aggiornare).
    /// </para>
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260805170000_NormalizeInitiativeMemberRole")]
    public partial class NormalizeInitiativeMemberRole : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.InitiativeMembers', 'U') IS NOT NULL
    UPDATE [dbo].[InitiativeMembers] SET [Role] = 1 WHERE [Role] = 0;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nessun ritorno possibile: quali righe fossero "responsabile" prima della
            // normalizzazione non e' piu' ricostruibile, e inventarlo sarebbe peggio che non farlo.
            // Il responsabile resta comunque leggibile da Initiatives.IdOwner.
        }
    }
}
