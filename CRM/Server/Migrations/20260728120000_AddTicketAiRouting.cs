using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Smistamento AI dei ticket verso i gruppi: competenze sui gruppi, esito del suggerimento sul
    /// ticket e tabella di configurazione dedicata.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260728120000_AddTicketAiRouting")]
    public partial class AddTicketAiRouting : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Competenze del gruppo: e' il testo su cui il modello sceglie.
            migrationBuilder.Sql(@"
IF COL_LENGTH('Groups','AiRoutingHints') IS NULL
    ALTER TABLE Groups ADD AiRoutingHints nvarchar(2000) NULL;
");

            // Esito dello smistamento sul ticket.
            migrationBuilder.Sql(@"
IF COL_LENGTH('Tickets','AiSuggestedGroupId') IS NULL
    ALTER TABLE Tickets ADD AiSuggestedGroupId int NULL;

IF COL_LENGTH('Tickets','AiRoutingConfidence') IS NULL
    ALTER TABLE Tickets ADD AiRoutingConfidence float NULL;

IF COL_LENGTH('Tickets','AiRoutingReason') IS NULL
    ALTER TABLE Tickets ADD AiRoutingReason nvarchar(2000) NULL;

IF COL_LENGTH('Tickets','AiRoutedAt') IS NULL
    ALTER TABLE Tickets ADD AiRoutedAt datetime2 NULL;

IF COL_LENGTH('Tickets','AiRoutingApplied') IS NULL
    ALTER TABLE Tickets ADD AiRoutingApplied bit NOT NULL CONSTRAINT DF_Tickets_AiRoutingApplied DEFAULT(0);

IF COL_LENGTH('Tickets','AiRoutingOutcome') IS NULL
    ALTER TABLE Tickets ADD AiRoutingOutcome int NOT NULL CONSTRAINT DF_Tickets_AiRoutingOutcome DEFAULT(0);
");

            // Indice e vincolo sul gruppo suggerito: batch separato perche' la colonna nasce sopra.
            migrationBuilder.Sql(@"
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Tickets_AiSuggestedGroupId' AND object_id=OBJECT_ID('Tickets'))
    CREATE INDEX IX_Tickets_AiSuggestedGroupId ON Tickets(AiSuggestedGroupId);

IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_Tickets_Groups_AiSuggestedGroupId')
    ALTER TABLE Tickets ADD CONSTRAINT FK_Tickets_Groups_AiSuggestedGroupId
        FOREIGN KEY (AiSuggestedGroupId) REFERENCES Groups(Id);
");

            // Configurazione dello smistamento: riga singola con chiave assegnata dal codice.
            migrationBuilder.Sql(@"
IF OBJECT_ID('TicketRoutingSettings','U') IS NULL
BEGIN
    CREATE TABLE TicketRoutingSettings (
        Id int NOT NULL,
        Enabled bit NOT NULL CONSTRAINT DF_TicketRoutingSettings_Enabled DEFAULT(0),
        AutoAssignThreshold float NOT NULL CONSTRAINT DF_TicketRoutingSettings_Threshold DEFAULT(0.75),
        RestrictToTicketTypeGroups bit NOT NULL CONSTRAINT DF_TicketRoutingSettings_Restrict DEFAULT(1),
        IdFallbackGroup int NULL,
        ApplyToEmailTickets bit NOT NULL CONSTRAINT DF_TicketRoutingSettings_Email DEFAULT(1),
        NotifyGroupOnAssign bit NOT NULL CONSTRAINT DF_TicketRoutingSettings_Notify DEFAULT(1),
        Model nvarchar(100) NULL,
        UpdatedAt datetime2 NULL,
        UpdatedBy nvarchar(450) NULL,
        CONSTRAINT PK_TicketRoutingSettings PRIMARY KEY (Id),
        CONSTRAINT FK_TicketRoutingSettings_Groups_IdFallbackGroup
            FOREIGN KEY (IdFallbackGroup) REFERENCES Groups(Id)
    );

    CREATE INDEX IX_TicketRoutingSettings_IdFallbackGroup ON TicketRoutingSettings(IdFallbackGroup);
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('TicketRoutingSettings','U') IS NOT NULL
    DROP TABLE TicketRoutingSettings;

IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_Tickets_Groups_AiSuggestedGroupId')
    ALTER TABLE Tickets DROP CONSTRAINT FK_Tickets_Groups_AiSuggestedGroupId;

IF EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Tickets_AiSuggestedGroupId' AND object_id=OBJECT_ID('Tickets'))
    DROP INDEX IX_Tickets_AiSuggestedGroupId ON Tickets;

IF COL_LENGTH('Tickets','AiRoutingOutcome') IS NOT NULL
BEGIN
    ALTER TABLE Tickets DROP CONSTRAINT DF_Tickets_AiRoutingOutcome;
    ALTER TABLE Tickets DROP COLUMN AiRoutingOutcome;
END

IF COL_LENGTH('Tickets','AiRoutingApplied') IS NOT NULL
BEGIN
    ALTER TABLE Tickets DROP CONSTRAINT DF_Tickets_AiRoutingApplied;
    ALTER TABLE Tickets DROP COLUMN AiRoutingApplied;
END

IF COL_LENGTH('Tickets','AiRoutedAt') IS NOT NULL
    ALTER TABLE Tickets DROP COLUMN AiRoutedAt;

IF COL_LENGTH('Tickets','AiRoutingReason') IS NOT NULL
    ALTER TABLE Tickets DROP COLUMN AiRoutingReason;

IF COL_LENGTH('Tickets','AiRoutingConfidence') IS NOT NULL
    ALTER TABLE Tickets DROP COLUMN AiRoutingConfidence;

IF COL_LENGTH('Tickets','AiSuggestedGroupId') IS NOT NULL
    ALTER TABLE Tickets DROP COLUMN AiSuggestedGroupId;

IF COL_LENGTH('Groups','AiRoutingHints') IS NOT NULL
    ALTER TABLE Groups DROP COLUMN AiRoutingHints;
");
        }
    }
}
