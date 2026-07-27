using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260726162000_AddTicketBlocking")]
    public partial class AddTicketBlocking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Tickets','IsBlocked') IS NULL
    ALTER TABLE Tickets ADD IsBlocked bit NOT NULL CONSTRAINT DF_Tickets_IsBlocked DEFAULT(0);

IF COL_LENGTH('Tickets','BlockReason') IS NULL
    ALTER TABLE Tickets ADD BlockReason nvarchar(2000) NULL;

IF COL_LENGTH('Tickets','BlockedAt') IS NULL
    ALTER TABLE Tickets ADD BlockedAt datetime2 NULL;

IF COL_LENGTH('Tickets','IdBlockedBy') IS NULL
    ALTER TABLE Tickets ADD IdBlockedBy nvarchar(450) NULL;

IF COL_LENGTH('Tickets','BlockResolvedAt') IS NULL
    ALTER TABLE Tickets ADD BlockResolvedAt datetime2 NULL;

IF COL_LENGTH('Tickets','IdBlockResolvedBy') IS NULL
    ALTER TABLE Tickets ADD IdBlockResolvedBy nvarchar(450) NULL;

IF COL_LENGTH('Tickets','BlockResolutionNote') IS NULL
    ALTER TABLE Tickets ADD BlockResolutionNote nvarchar(2000) NULL;

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Tickets_IsBlocked' AND object_id=OBJECT_ID('Tickets'))
    CREATE INDEX IX_Tickets_IsBlocked ON Tickets(IsBlocked);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Tickets_IdBlockedBy' AND object_id=OBJECT_ID('Tickets'))
    CREATE INDEX IX_Tickets_IdBlockedBy ON Tickets(IdBlockedBy);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Tickets_IdBlockResolvedBy' AND object_id=OBJECT_ID('Tickets'))
    CREATE INDEX IX_Tickets_IdBlockResolvedBy ON Tickets(IdBlockResolvedBy);

IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_Tickets_AspNetUsers_IdBlockedBy')
    ALTER TABLE Tickets ADD CONSTRAINT FK_Tickets_AspNetUsers_IdBlockedBy
        FOREIGN KEY (IdBlockedBy) REFERENCES AspNetUsers(Id);

IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_Tickets_AspNetUsers_IdBlockResolvedBy')
    ALTER TABLE Tickets ADD CONSTRAINT FK_Tickets_AspNetUsers_IdBlockResolvedBy
        FOREIGN KEY (IdBlockResolvedBy) REFERENCES AspNetUsers(Id);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @fk nvarchar(128);

SELECT @fk = fk.name
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
WHERE fk.parent_object_id = OBJECT_ID('Tickets') AND c.name = 'IdBlockedBy';

IF @fk IS NOT NULL
    EXEC('ALTER TABLE Tickets DROP CONSTRAINT ' + QUOTENAME(@fk));

SET @fk = NULL;
SELECT @fk = fk.name
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
WHERE fk.parent_object_id = OBJECT_ID('Tickets') AND c.name = 'IdBlockResolvedBy';

IF @fk IS NOT NULL
    EXEC('ALTER TABLE Tickets DROP CONSTRAINT ' + QUOTENAME(@fk));

IF EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Tickets_IdBlockResolvedBy' AND object_id=OBJECT_ID('Tickets'))
    DROP INDEX IX_Tickets_IdBlockResolvedBy ON Tickets;

IF EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Tickets_IdBlockedBy' AND object_id=OBJECT_ID('Tickets'))
    DROP INDEX IX_Tickets_IdBlockedBy ON Tickets;

IF EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Tickets_IsBlocked' AND object_id=OBJECT_ID('Tickets'))
    DROP INDEX IX_Tickets_IsBlocked ON Tickets;

IF COL_LENGTH('Tickets','BlockResolutionNote') IS NOT NULL
    ALTER TABLE Tickets DROP COLUMN BlockResolutionNote;

IF COL_LENGTH('Tickets','IdBlockResolvedBy') IS NOT NULL
    ALTER TABLE Tickets DROP COLUMN IdBlockResolvedBy;

IF COL_LENGTH('Tickets','BlockResolvedAt') IS NOT NULL
    ALTER TABLE Tickets DROP COLUMN BlockResolvedAt;

IF COL_LENGTH('Tickets','IdBlockedBy') IS NOT NULL
    ALTER TABLE Tickets DROP COLUMN IdBlockedBy;

IF COL_LENGTH('Tickets','BlockedAt') IS NOT NULL
    ALTER TABLE Tickets DROP COLUMN BlockedAt;

IF COL_LENGTH('Tickets','BlockReason') IS NOT NULL
    ALTER TABLE Tickets DROP COLUMN BlockReason;

IF COL_LENGTH('Tickets','IsBlocked') IS NOT NULL
BEGIN
    DECLARE @df nvarchar(128);
    SELECT @df = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID('Tickets') AND c.name = 'IsBlocked';

    IF @df IS NOT NULL
        EXEC('ALTER TABLE Tickets DROP CONSTRAINT ' + QUOTENAME(@df));

    ALTER TABLE Tickets DROP COLUMN IsBlocked;
END
");
        }
    }
}
