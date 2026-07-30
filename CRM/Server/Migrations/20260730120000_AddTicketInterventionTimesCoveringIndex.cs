using CRM.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Indice coprente sui tempi degli interventi: le ore di un ticket si calcolano sempre
    /// filtrando per intervento e per IsBillable e sommando le durate per tipo, e con il solo
    /// indice sulla FK ogni riga costava un key lookup su TimeType/orari. Il vecchio indice
    /// sulla sola FK viene rimosso: e' il prefisso di questo, quindi ridondante in lettura e
    /// solo peso in scrittura.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260730120000_AddTicketInterventionTimesCoveringIndex")]
    public partial class AddTicketInterventionTimesCoveringIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TicketInterventionTimes_IdTicketIntervention' AND object_id = OBJECT_ID('TicketInterventionTimes'))
    DROP INDEX IX_TicketInterventionTimes_IdTicketIntervention ON TicketInterventionTimes;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TicketInterventionTimes_IdTicketIntervention_IsBillable' AND object_id = OBJECT_ID('TicketInterventionTimes'))
    CREATE INDEX IX_TicketInterventionTimes_IdTicketIntervention_IsBillable
        ON TicketInterventionTimes (IdTicketIntervention, IsBillable)
        INCLUDE (TimeType, StartDateTime, EndDateTime);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TicketInterventionTimes_IdTicketIntervention_IsBillable' AND object_id = OBJECT_ID('TicketInterventionTimes'))
    DROP INDEX IX_TicketInterventionTimes_IdTicketIntervention_IsBillable ON TicketInterventionTimes;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TicketInterventionTimes_IdTicketIntervention' AND object_id = OBJECT_ID('TicketInterventionTimes'))
    CREATE INDEX IX_TicketInterventionTimes_IdTicketIntervention
        ON TicketInterventionTimes (IdTicketIntervention);
");
        }
    }
}
