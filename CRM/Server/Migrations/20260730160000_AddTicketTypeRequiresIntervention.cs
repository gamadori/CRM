using CRM.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Flag per tipo di ticket: la chiusura pretende almeno un intervento registrato.
    /// <para>
    /// Default 1 anche sulle righe esistenti: ore, trasferte, ricambi e rapportino vivono solo
    /// negli interventi, quindi il caso da rendere difficile e' chiudere senza registrare nulla.
    /// Chi ha tipi che si chiudono legittimamente a vuoto (duplicati, richieste informazioni) lo
    /// spegne su quei tipi.
    /// </para>
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260730160000_AddTicketTypeRequiresIntervention")]
    public partial class AddTicketTypeRequiresIntervention : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Il vincolo DEFAULT e' nominato: senza un nome esplicito SQL Server ne genera uno
            // casuale e il Down non saprebbe cosa rimuovere.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'RequiresIntervention' AND object_id = OBJECT_ID('TicketTypes'))
    ALTER TABLE TicketTypes ADD RequiresIntervention bit NOT NULL
        CONSTRAINT DF_TicketTypes_RequiresIntervention DEFAULT 1;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_TicketTypes_RequiresIntervention')
    ALTER TABLE TicketTypes DROP CONSTRAINT DF_TicketTypes_RequiresIntervention;
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'RequiresIntervention' AND object_id = OBJECT_ID('TicketTypes'))
    ALTER TABLE TicketTypes DROP COLUMN RequiresIntervention;
");
        }
    }
}
