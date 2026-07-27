using CRM.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Adds planned production tickets: templates on Gantt phases and per-commessa ticket plans.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260726130000_AddProductionTicketTemplatesAndPlans")]
    public partial class AddProductionTicketTemplatesAndPlans : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('GanttPhaseTicketTemplates') IS NULL
BEGIN
    CREATE TABLE GanttPhaseTicketTemplates
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_GanttPhaseTicketTemplates PRIMARY KEY,
        IdGanttPhase int NOT NULL,
        Title nvarchar(max) NOT NULL,
        Description nvarchar(max) NULL,
        IdTicketType int NOT NULL,
        IdGroupAssigned int NULL,
        Required bit NOT NULL CONSTRAINT DF_GanttPhaseTicketTemplates_Required DEFAULT(1),
        AutoCreateMode int NOT NULL CONSTRAINT DF_GanttPhaseTicketTemplates_AutoCreateMode DEFAULT(1),
        SortOrder int NOT NULL CONSTRAINT DF_GanttPhaseTicketTemplates_SortOrder DEFAULT(0),
        CONSTRAINT FK_GanttPhaseTicketTemplates_GanttPhases_IdGanttPhase
            FOREIGN KEY (IdGanttPhase) REFERENCES GanttPhases(Id) ON DELETE CASCADE,
        CONSTRAINT FK_GanttPhaseTicketTemplates_TicketTypes_IdTicketType
            FOREIGN KEY (IdTicketType) REFERENCES TicketTypes(Id),
        CONSTRAINT FK_GanttPhaseTicketTemplates_Groups_IdGroupAssigned
            FOREIGN KEY (IdGroupAssigned) REFERENCES [Groups](Id)
    );

    CREATE INDEX IX_GanttPhaseTicketTemplates_IdGanttPhase ON GanttPhaseTicketTemplates(IdGanttPhase);
    CREATE INDEX IX_GanttPhaseTicketTemplates_IdTicketType ON GanttPhaseTicketTemplates(IdTicketType);
    CREATE INDEX IX_GanttPhaseTicketTemplates_IdGroupAssigned ON GanttPhaseTicketTemplates(IdGroupAssigned);
END

IF OBJECT_ID('CommessaFaseTicketPlans') IS NULL
BEGIN
    CREATE TABLE CommessaFaseTicketPlans
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CommessaFaseTicketPlans PRIMARY KEY,
        IdCommessaFase int NOT NULL,
        IdGanttPhaseTicketTemplate int NULL,
        Title nvarchar(max) NOT NULL,
        Description nvarchar(max) NULL,
        IdTicketType int NOT NULL,
        IdGroupAssigned int NULL,
        Required bit NOT NULL CONSTRAINT DF_CommessaFaseTicketPlans_Required DEFAULT(1),
        AutoCreateMode int NOT NULL CONSTRAINT DF_CommessaFaseTicketPlans_AutoCreateMode DEFAULT(1),
        SortOrder int NOT NULL CONSTRAINT DF_CommessaFaseTicketPlans_SortOrder DEFAULT(0),
        IdTicket int NULL,
        CONSTRAINT FK_CommessaFaseTicketPlans_CommessaFasi_IdCommessaFase
            FOREIGN KEY (IdCommessaFase) REFERENCES CommessaFasi(Id) ON DELETE CASCADE,
        CONSTRAINT FK_CommessaFaseTicketPlans_GanttPhaseTicketTemplates_IdGanttPhaseTicketTemplate
            FOREIGN KEY (IdGanttPhaseTicketTemplate) REFERENCES GanttPhaseTicketTemplates(Id),
        CONSTRAINT FK_CommessaFaseTicketPlans_TicketTypes_IdTicketType
            FOREIGN KEY (IdTicketType) REFERENCES TicketTypes(Id),
        CONSTRAINT FK_CommessaFaseTicketPlans_Groups_IdGroupAssigned
            FOREIGN KEY (IdGroupAssigned) REFERENCES [Groups](Id),
        CONSTRAINT FK_CommessaFaseTicketPlans_Tickets_IdTicket
            FOREIGN KEY (IdTicket) REFERENCES Tickets(Id) ON DELETE SET NULL
    );

    CREATE INDEX IX_CommessaFaseTicketPlans_IdCommessaFase ON CommessaFaseTicketPlans(IdCommessaFase);
    CREATE INDEX IX_CommessaFaseTicketPlans_IdGanttPhaseTicketTemplate ON CommessaFaseTicketPlans(IdGanttPhaseTicketTemplate);
    CREATE INDEX IX_CommessaFaseTicketPlans_IdTicketType ON CommessaFaseTicketPlans(IdTicketType);
    CREATE INDEX IX_CommessaFaseTicketPlans_IdGroupAssigned ON CommessaFaseTicketPlans(IdGroupAssigned);
    CREATE INDEX IX_CommessaFaseTicketPlans_IdTicket ON CommessaFaseTicketPlans(IdTicket);
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('CommessaFaseTicketPlans') IS NOT NULL
    DROP TABLE CommessaFaseTicketPlans;

IF OBJECT_ID('GanttPhaseTicketTemplates') IS NOT NULL
    DROP TABLE GanttPhaseTicketTemplates;
");
        }
    }
}
