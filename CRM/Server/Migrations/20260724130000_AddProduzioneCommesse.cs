using CRM.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Modello produzione MTO: rimuove il modello "Project" errato e introduce Commesse,
    /// CommessaFasi (+dipendenze), GanttPhases template (+dipendenze), OrderRow.ProductionStatus,
    /// Ticket.IdCommessaFase; rimuove GanttPlan.Kind. T-SQL a batch separati (tool ef design rotto).
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260724130000_AddProduzioneCommesse")]
    public partial class AddProduzioneCommesse : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Batch 1: rimozione modello sbagliato (FK/indici/colonne/tabelle)
            migrationBuilder.Sql(@"
-- Tickets.IdProjectTask
DECLARE @fk sysname;
SELECT @fk = fk.name FROM sys.foreign_keys fk WHERE fk.parent_object_id=OBJECT_ID('Tickets')
  AND EXISTS(SELECT 1 FROM sys.foreign_key_columns c JOIN sys.columns col ON col.object_id=c.parent_object_id AND col.column_id=c.parent_column_id WHERE c.constraint_object_id=fk.object_id AND col.name='IdProjectTask');
IF @fk IS NOT NULL EXEC('ALTER TABLE Tickets DROP CONSTRAINT ' + @fk);
IF EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Tickets_IdProjectTask' AND object_id=OBJECT_ID('Tickets')) DROP INDEX IX_Tickets_IdProjectTask ON Tickets;
IF COL_LENGTH('Tickets','IdProjectTask') IS NOT NULL ALTER TABLE Tickets DROP COLUMN IdProjectTask;

-- Orders.IdProject
DECLARE @fko sysname;
SELECT @fko = fk.name FROM sys.foreign_keys fk WHERE fk.parent_object_id=OBJECT_ID('Orders')
  AND EXISTS(SELECT 1 FROM sys.foreign_key_columns c JOIN sys.columns col ON col.object_id=c.parent_object_id AND col.column_id=c.parent_column_id WHERE c.constraint_object_id=fk.object_id AND col.name='IdProject');
IF @fko IS NOT NULL EXEC('ALTER TABLE Orders DROP CONSTRAINT ' + @fko);
IF EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Orders_IdProject' AND object_id=OBJECT_ID('Orders')) DROP INDEX IX_Orders_IdProject ON Orders;
IF COL_LENGTH('Orders','IdProject') IS NOT NULL ALTER TABLE Orders DROP COLUMN IdProject;

-- Tickets.IdProject (FK vecchia verso Projects)
DECLARE @fkp sysname;
SELECT @fkp = fk.name FROM sys.foreign_keys fk WHERE fk.parent_object_id=OBJECT_ID('Tickets')
  AND EXISTS(SELECT 1 FROM sys.foreign_key_columns c JOIN sys.columns col ON col.object_id=c.parent_object_id AND col.column_id=c.parent_column_id WHERE c.constraint_object_id=fk.object_id AND col.name='IdProject');
IF @fkp IS NOT NULL EXEC('ALTER TABLE Tickets DROP CONSTRAINT ' + @fkp);
IF EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Tickets_IdProject' AND object_id=OBJECT_ID('Tickets')) DROP INDEX IX_Tickets_IdProject ON Tickets;
IF COL_LENGTH('Tickets','IdProject') IS NOT NULL ALTER TABLE Tickets DROP COLUMN IdProject;

-- Sicurezza: elimina qualunque FK residua che referenzia Projects
DECLARE @sql nvarchar(max) = N'';
IF OBJECT_ID('Projects') IS NOT NULL
BEGIN
    SELECT @sql = @sql + 'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + '.' + QUOTENAME(OBJECT_NAME(fk.parent_object_id)) + ' DROP CONSTRAINT ' + QUOTENAME(fk.name) + ';'
    FROM sys.foreign_keys fk WHERE fk.referenced_object_id = OBJECT_ID('Projects');
    IF LEN(@sql) > 0 EXEC sp_executesql @sql;
END

-- Tabelle del modello sbagliato
IF OBJECT_ID('ProjectTaskDependencies') IS NOT NULL DROP TABLE ProjectTaskDependencies;
IF OBJECT_ID('ProjectTasks') IS NOT NULL DROP TABLE ProjectTasks;
IF OBJECT_ID('ProjectUsers') IS NOT NULL DROP TABLE ProjectUsers;
IF OBJECT_ID('Projects') IS NOT NULL DROP TABLE Projects;
");

            // Batch 2: colonne aggiuntive (no referenze nello stesso batch)
            migrationBuilder.Sql(@"
IF COL_LENGTH('OrderRows','ProductionStatus') IS NULL ALTER TABLE OrderRows ADD ProductionStatus int NOT NULL DEFAULT(0);
IF COL_LENGTH('Tickets','IdCommessaFase') IS NULL ALTER TABLE Tickets ADD IdCommessaFase int NULL;
IF COL_LENGTH('GanttPlans','Kind') IS NOT NULL ALTER TABLE GanttPlans DROP COLUMN Kind;
");

            // Batch 3: Commesse
            migrationBuilder.Sql(@"
IF OBJECT_ID('Commesse') IS NULL
CREATE TABLE Commesse (
    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Commesse PRIMARY KEY,
    Code nvarchar(max) NULL,
    IdOrderRow int NULL,
    IdCompany int NULL,
    IdProduct int NULL,
    IdArticle int NULL,
    IdGanttPlan int NULL,
    Name nvarchar(max) NULL,
    Description nvarchar(max) NULL,
    Note nvarchar(max) NULL,
    State int NOT NULL,
    Priority int NOT NULL,
    StartDatePlanned datetime2 NOT NULL,
    EndDatePlanned datetime2 NOT NULL,
    StartDateActual datetime2 NULL,
    EndDateActual datetime2 NULL,
    Progress int NOT NULL,
    BudgetHours int NULL,
    IdUserResponsible nvarchar(450) NULL,
    IdUserCreate nvarchar(450) NULL,
    CreatedAt datetime2 NOT NULL,
    CONSTRAINT FK_Commesse_OrderRows_IdOrderRow FOREIGN KEY (IdOrderRow) REFERENCES OrderRows(Id) ON DELETE SET NULL
);
IF OBJECT_ID('Commesse') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Commesse_IdOrderRow' AND object_id=OBJECT_ID('Commesse'))
    CREATE INDEX IX_Commesse_IdOrderRow ON Commesse(IdOrderRow);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Commesse_IdCompany' AND object_id=OBJECT_ID('Commesse'))
    CREATE INDEX IX_Commesse_IdCompany ON Commesse(IdCompany);
");

            // Batch 4: CommessaFasi + dipendenze
            migrationBuilder.Sql(@"
IF OBJECT_ID('CommessaFasi') IS NULL
CREATE TABLE CommessaFasi (
    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CommessaFasi PRIMARY KEY,
    IdCommessa int NOT NULL,
    ParentId int NULL,
    Name nvarchar(max) NOT NULL,
    Description nvarchar(max) NULL,
    StartDate datetime2 NOT NULL,
    EndDate datetime2 NOT NULL,
    Progress int NOT NULL,
    SortOrder int NOT NULL,
    IsMilestone bit NOT NULL,
    Color nvarchar(max) NULL,
    State int NOT NULL,
    IdTicketType int NULL,
    IdGroup int NULL,
    IdUserTakenBy nvarchar(450) NULL,
    TakenAt datetime2 NULL,
    CONSTRAINT FK_CommessaFasi_Commesse_IdCommessa FOREIGN KEY (IdCommessa) REFERENCES Commesse(Id) ON DELETE CASCADE,
    CONSTRAINT FK_CommessaFasi_CommessaFasi_ParentId FOREIGN KEY (ParentId) REFERENCES CommessaFasi(Id)
);
IF OBJECT_ID('CommessaFasi') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_CommessaFasi_IdCommessa' AND object_id=OBJECT_ID('CommessaFasi'))
    CREATE INDEX IX_CommessaFasi_IdCommessa ON CommessaFasi(IdCommessa);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_CommessaFasi_ParentId' AND object_id=OBJECT_ID('CommessaFasi'))
    CREATE INDEX IX_CommessaFasi_ParentId ON CommessaFasi(ParentId);

IF OBJECT_ID('CommessaFaseDependencies') IS NULL
CREATE TABLE CommessaFaseDependencies (
    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CommessaFaseDependencies PRIMARY KEY,
    IdFase int NOT NULL,
    IdPredecessorFase int NOT NULL,
    LagDays int NOT NULL,
    Type int NOT NULL,
    CONSTRAINT FK_CommessaFaseDependencies_CommessaFasi_IdFase FOREIGN KEY (IdFase) REFERENCES CommessaFasi(Id),
    CONSTRAINT FK_CommessaFaseDependencies_CommessaFasi_IdPredecessorFase FOREIGN KEY (IdPredecessorFase) REFERENCES CommessaFasi(Id)
);
IF OBJECT_ID('CommessaFaseDependencies') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_CommessaFaseDependencies_IdFase' AND object_id=OBJECT_ID('CommessaFaseDependencies'))
    CREATE INDEX IX_CommessaFaseDependencies_IdFase ON CommessaFaseDependencies(IdFase);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_CommessaFaseDependencies_IdPredecessorFase' AND object_id=OBJECT_ID('CommessaFaseDependencies'))
    CREATE INDEX IX_CommessaFaseDependencies_IdPredecessorFase ON CommessaFaseDependencies(IdPredecessorFase);
");

            // Batch 5: GanttPhases + dipendenze
            migrationBuilder.Sql(@"
IF OBJECT_ID('GanttPhases') IS NULL
CREATE TABLE GanttPhases (
    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_GanttPhases PRIMARY KEY,
    IdGanttPlan int NOT NULL,
    ParentId int NULL,
    Name nvarchar(max) NOT NULL,
    Description nvarchar(max) NULL,
    DurationDays int NOT NULL,
    SortOrder int NOT NULL,
    IsMilestone bit NOT NULL,
    IdTicketType int NULL,
    IdGroup int NULL,
    Color nvarchar(max) NULL,
    CONSTRAINT FK_GanttPhases_GanttPlans_IdGanttPlan FOREIGN KEY (IdGanttPlan) REFERENCES GanttPlans(Id) ON DELETE CASCADE,
    CONSTRAINT FK_GanttPhases_GanttPhases_ParentId FOREIGN KEY (ParentId) REFERENCES GanttPhases(Id)
);
IF OBJECT_ID('GanttPhases') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_GanttPhases_IdGanttPlan' AND object_id=OBJECT_ID('GanttPhases'))
    CREATE INDEX IX_GanttPhases_IdGanttPlan ON GanttPhases(IdGanttPlan);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_GanttPhases_ParentId' AND object_id=OBJECT_ID('GanttPhases'))
    CREATE INDEX IX_GanttPhases_ParentId ON GanttPhases(ParentId);

IF OBJECT_ID('GanttPhaseDependencies') IS NULL
CREATE TABLE GanttPhaseDependencies (
    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_GanttPhaseDependencies PRIMARY KEY,
    IdPhase int NOT NULL,
    IdPredecessorPhase int NOT NULL,
    LagDays int NOT NULL,
    Type int NOT NULL,
    CONSTRAINT FK_GanttPhaseDependencies_GanttPhases_IdPhase FOREIGN KEY (IdPhase) REFERENCES GanttPhases(Id),
    CONSTRAINT FK_GanttPhaseDependencies_GanttPhases_IdPredecessorPhase FOREIGN KEY (IdPredecessorPhase) REFERENCES GanttPhases(Id)
);
IF OBJECT_ID('GanttPhaseDependencies') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_GanttPhaseDependencies_IdPhase' AND object_id=OBJECT_ID('GanttPhaseDependencies'))
    CREATE INDEX IX_GanttPhaseDependencies_IdPhase ON GanttPhaseDependencies(IdPhase);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_GanttPhaseDependencies_IdPredecessorPhase' AND object_id=OBJECT_ID('GanttPhaseDependencies'))
    CREATE INDEX IX_GanttPhaseDependencies_IdPredecessorPhase ON GanttPhaseDependencies(IdPredecessorPhase);
");

            // Batch 6: FK/indice Tickets.IdCommessaFase (dopo che CommessaFasi esiste)
            migrationBuilder.Sql(@"
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Tickets_IdCommessaFase' AND object_id=OBJECT_ID('Tickets'))
    CREATE INDEX IX_Tickets_IdCommessaFase ON Tickets(IdCommessaFase);
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_Tickets_CommessaFasi_IdCommessaFase')
    ALTER TABLE Tickets ADD CONSTRAINT FK_Tickets_CommessaFasi_IdCommessaFase FOREIGN KEY (IdCommessaFase) REFERENCES CommessaFasi(Id);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @fk sysname;
SELECT @fk = fk.name FROM sys.foreign_keys fk WHERE fk.parent_object_id=OBJECT_ID('Tickets')
  AND EXISTS(SELECT 1 FROM sys.foreign_key_columns c JOIN sys.columns col ON col.object_id=c.parent_object_id AND col.column_id=c.parent_column_id WHERE c.constraint_object_id=fk.object_id AND col.name='IdCommessaFase');
IF @fk IS NOT NULL EXEC('ALTER TABLE Tickets DROP CONSTRAINT ' + @fk);
IF EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Tickets_IdCommessaFase' AND object_id=OBJECT_ID('Tickets')) DROP INDEX IX_Tickets_IdCommessaFase ON Tickets;
IF COL_LENGTH('Tickets','IdCommessaFase') IS NOT NULL ALTER TABLE Tickets DROP COLUMN IdCommessaFase;

IF OBJECT_ID('CommessaFaseDependencies') IS NOT NULL DROP TABLE CommessaFaseDependencies;
IF OBJECT_ID('CommessaFasi') IS NOT NULL DROP TABLE CommessaFasi;
IF OBJECT_ID('Commesse') IS NOT NULL DROP TABLE Commesse;
IF OBJECT_ID('GanttPhaseDependencies') IS NOT NULL DROP TABLE GanttPhaseDependencies;
IF OBJECT_ID('GanttPhases') IS NOT NULL DROP TABLE GanttPhases;

IF COL_LENGTH('OrderRows','ProductionStatus') IS NOT NULL ALTER TABLE OrderRows DROP COLUMN ProductionStatus;
IF COL_LENGTH('GanttPlans','Kind') IS NULL ALTER TABLE GanttPlans ADD Kind int NOT NULL DEFAULT(0);
");
        }
    }
}
