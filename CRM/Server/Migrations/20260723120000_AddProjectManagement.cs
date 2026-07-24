using CRM.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260723120000_AddProjectManagement")]
    /// <summary>
    /// Modulo Progetti "commessa operativa": nuove colonne su Projects, tabelle ProjectTasks e
    /// ProjectTaskDependencies (Gantt), Orders.IdProject (uno-a-molti), Tickets.IdProjectTask con
    /// rimozione di Tickets.IdOrder (backfill su Projects), ruolo su ProjectUsers. Rimuove le
    /// tabelle legacy del vecchio Gantt (TasksProject, ProjectModels).
    ///
    /// Scritta in T-SQL esplicito perché l'ambiente .NET 10 corrente ha un difetto degli strumenti
    /// di design EF (conflitto Roslyn) che impedisce la generazione automatica; le operazioni sono
    /// idempotenti e applicabili via Update-Database.
    /// </summary>
    public partial class AddProjectManagement : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NB: ogni Sql() e' un batch separato. SQL Server valida l'intero batch prima
            // di eseguirlo, quindi le colonne appena aggiunte non possono essere referenziate
            // nello stesso batch: si suddivide in piu' batch ordinati.

            // Batch 1: rename + drop legacy + aggiunta colonne su Projects
            migrationBuilder.Sql(@"
IF COL_LENGTH('Projects','StartDate') IS NOT NULL AND COL_LENGTH('Projects','StartDatePlanned') IS NULL
    EXEC sp_rename 'Projects.StartDate','StartDatePlanned','COLUMN';
IF COL_LENGTH('Projects','EndDate') IS NOT NULL AND COL_LENGTH('Projects','EndDatePlanned') IS NULL
    EXEC sp_rename 'Projects.EndDate','EndDatePlanned','COLUMN';

DECLARE @fk sysname;
SELECT @fk = fk.name FROM sys.foreign_keys fk
 WHERE fk.parent_object_id = OBJECT_ID('Projects')
   AND EXISTS (SELECT 1 FROM sys.foreign_key_columns fkc
               JOIN sys.columns c ON c.object_id=fkc.parent_object_id AND c.column_id=fkc.parent_column_id
               WHERE fkc.constraint_object_id=fk.object_id AND c.name='IdProduct');
IF @fk IS NOT NULL EXEC('ALTER TABLE Projects DROP CONSTRAINT ' + @fk);
IF EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Projects_IdProduct' AND object_id=OBJECT_ID('Projects'))
    DROP INDEX IX_Projects_IdProduct ON Projects;
IF COL_LENGTH('Projects','IdProduct') IS NOT NULL ALTER TABLE Projects DROP COLUMN IdProduct;
IF COL_LENGTH('Projects','DurationHours') IS NOT NULL ALTER TABLE Projects DROP COLUMN DurationHours;

IF COL_LENGTH('Projects','Code') IS NULL ALTER TABLE Projects ADD Code nvarchar(max) NULL;
IF COL_LENGTH('Projects','IdContact') IS NULL ALTER TABLE Projects ADD IdContact int NULL;
IF COL_LENGTH('Projects','CreatedAt') IS NULL ALTER TABLE Projects ADD CreatedAt datetime2 NOT NULL DEFAULT (SYSDATETIME());
IF COL_LENGTH('Projects','StartDateActual') IS NULL ALTER TABLE Projects ADD StartDateActual datetime2 NULL;
IF COL_LENGTH('Projects','EndDateActual') IS NULL ALTER TABLE Projects ADD EndDateActual datetime2 NULL;
IF COL_LENGTH('Projects','Progress') IS NULL ALTER TABLE Projects ADD Progress int NOT NULL DEFAULT(0);
IF COL_LENGTH('Projects','BudgetHours') IS NULL ALTER TABLE Projects ADD BudgetHours int NULL;
");

            // Batch 2: index/FK IdContact, ProjectUsers.Role, tabelle Gantt, Orders.IdProject (colonna)
            migrationBuilder.Sql(@"
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Projects_IdContact' AND object_id=OBJECT_ID('Projects'))
    CREATE INDEX IX_Projects_IdContact ON Projects(IdContact);
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_Projects_Contacts_IdContact')
    ALTER TABLE Projects ADD CONSTRAINT FK_Projects_Contacts_IdContact FOREIGN KEY (IdContact) REFERENCES Contacts(Id);

IF COL_LENGTH('ProjectUsers','Role') IS NULL ALTER TABLE ProjectUsers ADD Role int NOT NULL DEFAULT(0);

IF OBJECT_ID('ProjectTasks') IS NULL
BEGIN
    CREATE TABLE ProjectTasks (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProjectTasks PRIMARY KEY,
        IdProject int NOT NULL,
        ParentId int NULL,
        Name nvarchar(max) NOT NULL,
        Description nvarchar(max) NULL,
        StartDate datetime2 NOT NULL,
        EndDate datetime2 NOT NULL,
        Progress int NOT NULL,
        SortOrder int NOT NULL,
        IsMilestone bit NOT NULL,
        Color nvarchar(max) NULL,
        CONSTRAINT FK_ProjectTasks_Projects_IdProject FOREIGN KEY (IdProject) REFERENCES Projects(Id) ON DELETE CASCADE,
        CONSTRAINT FK_ProjectTasks_ProjectTasks_ParentId FOREIGN KEY (ParentId) REFERENCES ProjectTasks(Id)
    );
    CREATE INDEX IX_ProjectTasks_IdProject ON ProjectTasks(IdProject);
    CREATE INDEX IX_ProjectTasks_ParentId ON ProjectTasks(ParentId);
END

IF OBJECT_ID('ProjectTaskDependencies') IS NULL
BEGIN
    CREATE TABLE ProjectTaskDependencies (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProjectTaskDependencies PRIMARY KEY,
        IdTask int NOT NULL,
        IdPredecessorTask int NOT NULL,
        LagDays int NOT NULL,
        Type int NOT NULL,
        CONSTRAINT FK_ProjectTaskDependencies_ProjectTasks_IdTask FOREIGN KEY (IdTask) REFERENCES ProjectTasks(Id),
        CONSTRAINT FK_ProjectTaskDependencies_ProjectTasks_IdPredecessorTask FOREIGN KEY (IdPredecessorTask) REFERENCES ProjectTasks(Id)
    );
    CREATE INDEX IX_ProjectTaskDependencies_IdTask ON ProjectTaskDependencies(IdTask);
    CREATE INDEX IX_ProjectTaskDependencies_IdPredecessorTask ON ProjectTaskDependencies(IdPredecessorTask);
END

IF COL_LENGTH('Orders','IdProject') IS NULL ALTER TABLE Orders ADD IdProject int NULL;
");

            // Batch 3: index/FK su Orders.IdProject
            migrationBuilder.Sql(@"
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Orders_IdProject' AND object_id=OBJECT_ID('Orders'))
    CREATE INDEX IX_Orders_IdProject ON Orders(IdProject);
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_Orders_Projects_IdProject')
    ALTER TABLE Orders ADD CONSTRAINT FK_Orders_Projects_IdProject FOREIGN KEY (IdProject) REFERENCES Projects(Id) ON DELETE SET NULL;
");

            // Batch 4: backfill (referenzia le colonne aggiunte nei batch precedenti)
            migrationBuilder.Sql(@"
IF COL_LENGTH('Tickets','IdOrder') IS NOT NULL
BEGIN
    DECLARE @oid int, @onum nvarchar(450), @ocomp int, @ocont int, @ouser nvarchar(450), @odel datetime2, @pid int;
    DECLARE @yr varchar(4) = CONVERT(varchar,YEAR(GETDATE()));
    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
        SELECT o.Id, o.Number, o.IdCompany, o.IdContact, o.IdUser, o.DeliveryDate
        FROM Orders o
        WHERE o.IdProject IS NULL AND EXISTS (SELECT 1 FROM Tickets t WHERE t.IdOrder = o.Id);
    OPEN cur;
    FETCH NEXT FROM cur INTO @oid,@onum,@ocomp,@ocont,@ouser,@odel;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        INSERT INTO Projects (Code, Name, IdCompany, IdContact, IdUserLeader, IdUserCreate, CreatedAt, State, StartDatePlanned, EndDatePlanned, Progress)
        VALUES ('PRJ-'+@yr+'-M'+RIGHT('0000'+CONVERT(varchar,@oid),4),
                'Commessa '+ISNULL(@onum,CONVERT(varchar,@oid)),
                @ocomp, @ocont, @ouser, @ouser, SYSDATETIME(), 1,
                CAST(GETDATE() AS datetime2), ISNULL(@odel, DATEADD(day,30,GETDATE())), 0);
        SET @pid = SCOPE_IDENTITY();
        UPDATE Orders  SET IdProject = @pid WHERE Id = @oid;
        UPDATE Tickets SET IdProject = @pid WHERE IdOrder = @oid;
        FETCH NEXT FROM cur INTO @oid,@onum,@ocomp,@ocont,@ouser,@odel;
    END
    CLOSE cur; DEALLOCATE cur;
END
");

            // Batch 5: Tickets.IdProjectTask, rimozione IdOrder, drop tabelle legacy
            migrationBuilder.Sql(@"
IF COL_LENGTH('Tickets','IdProjectTask') IS NULL ALTER TABLE Tickets ADD IdProjectTask int NULL;
");
            migrationBuilder.Sql(@"
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Tickets_IdProjectTask' AND object_id=OBJECT_ID('Tickets'))
    CREATE INDEX IX_Tickets_IdProjectTask ON Tickets(IdProjectTask);
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_Tickets_ProjectTasks_IdProjectTask')
    ALTER TABLE Tickets ADD CONSTRAINT FK_Tickets_ProjectTasks_IdProjectTask FOREIGN KEY (IdProjectTask) REFERENCES ProjectTasks(Id);

DECLARE @fko sysname;
SELECT @fko = fk.name FROM sys.foreign_keys fk
 WHERE fk.parent_object_id = OBJECT_ID('Tickets')
   AND EXISTS (SELECT 1 FROM sys.foreign_key_columns fkc
               JOIN sys.columns c ON c.object_id=fkc.parent_object_id AND c.column_id=fkc.parent_column_id
               WHERE fkc.constraint_object_id=fk.object_id AND c.name='IdOrder');
IF @fko IS NOT NULL EXEC('ALTER TABLE Tickets DROP CONSTRAINT ' + @fko);
IF EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Tickets_IdOrder' AND object_id=OBJECT_ID('Tickets'))
    DROP INDEX IX_Tickets_IdOrder ON Tickets;
IF COL_LENGTH('Tickets','IdOrder') IS NOT NULL ALTER TABLE Tickets DROP COLUMN IdOrder;

IF OBJECT_ID('TasksProject') IS NOT NULL DROP TABLE TasksProject;
IF OBJECT_ID('ProjectModels') IS NOT NULL DROP TABLE ProjectModels;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- Rollback strutturale (i dati delle commesse generate dal backfill non vengono ripristinati)
IF OBJECT_ID('ProjectTaskDependencies') IS NOT NULL DROP TABLE ProjectTaskDependencies;

DECLARE @fkt sysname;
SELECT @fkt = fk.name FROM sys.foreign_keys fk
 WHERE fk.parent_object_id = OBJECT_ID('Tickets')
   AND EXISTS (SELECT 1 FROM sys.foreign_key_columns fkc
               JOIN sys.columns c ON c.object_id=fkc.parent_object_id AND c.column_id=fkc.parent_column_id
               WHERE fkc.constraint_object_id=fk.object_id AND c.name='IdProjectTask');
IF @fkt IS NOT NULL EXEC('ALTER TABLE Tickets DROP CONSTRAINT ' + @fkt);
IF EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Tickets_IdProjectTask' AND object_id=OBJECT_ID('Tickets'))
    DROP INDEX IX_Tickets_IdProjectTask ON Tickets;
IF COL_LENGTH('Tickets','IdProjectTask') IS NOT NULL ALTER TABLE Tickets DROP COLUMN IdProjectTask;
IF COL_LENGTH('Tickets','IdOrder') IS NULL ALTER TABLE Tickets ADD IdOrder int NULL;

IF OBJECT_ID('ProjectTasks') IS NOT NULL DROP TABLE ProjectTasks;

DECLARE @fko2 sysname;
SELECT @fko2 = fk.name FROM sys.foreign_keys fk
 WHERE fk.parent_object_id = OBJECT_ID('Orders')
   AND EXISTS (SELECT 1 FROM sys.foreign_key_columns fkc
               JOIN sys.columns c ON c.object_id=fkc.parent_object_id AND c.column_id=fkc.parent_column_id
               WHERE fkc.constraint_object_id=fk.object_id AND c.name='IdProject');
IF @fko2 IS NOT NULL EXEC('ALTER TABLE Orders DROP CONSTRAINT ' + @fko2);
IF EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Orders_IdProject' AND object_id=OBJECT_ID('Orders'))
    DROP INDEX IX_Orders_IdProject ON Orders;
IF COL_LENGTH('Orders','IdProject') IS NOT NULL ALTER TABLE Orders DROP COLUMN IdProject;

IF COL_LENGTH('ProjectUsers','Role') IS NOT NULL ALTER TABLE ProjectUsers DROP COLUMN Role;

DECLARE @fkc sysname;
SELECT @fkc = fk.name FROM sys.foreign_keys fk
 WHERE fk.parent_object_id = OBJECT_ID('Projects')
   AND EXISTS (SELECT 1 FROM sys.foreign_key_columns fkc
               JOIN sys.columns c ON c.object_id=fkc.parent_object_id AND c.column_id=fkc.parent_column_id
               WHERE fkc.constraint_object_id=fk.object_id AND c.name='IdContact');
IF @fkc IS NOT NULL EXEC('ALTER TABLE Projects DROP CONSTRAINT ' + @fkc);
IF EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Projects_IdContact' AND object_id=OBJECT_ID('Projects'))
    DROP INDEX IX_Projects_IdContact ON Projects;

IF COL_LENGTH('Projects','BudgetHours') IS NOT NULL ALTER TABLE Projects DROP COLUMN BudgetHours;
IF COL_LENGTH('Projects','Progress') IS NOT NULL ALTER TABLE Projects DROP COLUMN Progress;
IF COL_LENGTH('Projects','EndDateActual') IS NOT NULL ALTER TABLE Projects DROP COLUMN EndDateActual;
IF COL_LENGTH('Projects','StartDateActual') IS NOT NULL ALTER TABLE Projects DROP COLUMN StartDateActual;
IF COL_LENGTH('Projects','CreatedAt') IS NOT NULL ALTER TABLE Projects DROP COLUMN CreatedAt;
IF COL_LENGTH('Projects','IdContact') IS NOT NULL ALTER TABLE Projects DROP COLUMN IdContact;
IF COL_LENGTH('Projects','Code') IS NOT NULL ALTER TABLE Projects DROP COLUMN Code;
IF COL_LENGTH('Projects','DurationHours') IS NULL ALTER TABLE Projects ADD DurationHours int NOT NULL DEFAULT(0);
IF COL_LENGTH('Projects','StartDatePlanned') IS NOT NULL AND COL_LENGTH('Projects','StartDate') IS NULL
    EXEC sp_rename 'Projects.StartDatePlanned','StartDate','COLUMN';
IF COL_LENGTH('Projects','EndDatePlanned') IS NOT NULL AND COL_LENGTH('Projects','EndDate') IS NULL
    EXEC sp_rename 'Projects.EndDatePlanned','EndDate','COLUMN';
");
        }
    }
}
