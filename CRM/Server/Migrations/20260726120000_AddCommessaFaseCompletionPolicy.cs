using CRM.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Adds per-phase completion policy so a commessa phase can be manual or driven by one/many tickets.
    /// Defaults preserve the previous behavior: phases with tickets complete when all tickets are closed.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260726120000_AddCommessaFaseCompletionPolicy")]
    public partial class AddCommessaFaseCompletionPolicy : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('CommessaFasi') IS NOT NULL AND COL_LENGTH('CommessaFasi','CompletionMode') IS NULL
    ALTER TABLE CommessaFasi ADD CompletionMode int NOT NULL CONSTRAINT DF_CommessaFasi_CompletionMode DEFAULT(1);

IF OBJECT_ID('CommessaFasi') IS NOT NULL AND COL_LENGTH('CommessaFasi','AutoCreateTicketOnTake') IS NULL
    ALTER TABLE CommessaFasi ADD AutoCreateTicketOnTake bit NOT NULL CONSTRAINT DF_CommessaFasi_AutoCreateTicketOnTake DEFAULT(1);

IF OBJECT_ID('CommessaFasi') IS NOT NULL AND COL_LENGTH('CommessaFasi','RequiresTicket') IS NULL
    ALTER TABLE CommessaFasi ADD RequiresTicket bit NOT NULL CONSTRAINT DF_CommessaFasi_RequiresTicket DEFAULT(1);

IF OBJECT_ID('CommessaFasi') IS NOT NULL
BEGIN
    EXEC('UPDATE CommessaFasi
             SET CompletionMode = 0,
                 AutoCreateTicketOnTake = 0,
                 RequiresTicket = 0
           WHERE IdTicketType IS NULL');
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('CommessaFasi') IS NOT NULL AND COL_LENGTH('CommessaFasi','RequiresTicket') IS NOT NULL
BEGIN
    DECLARE @dfRequires sysname;
    SELECT @dfRequires = dc.name
      FROM sys.default_constraints dc
      JOIN sys.columns c ON c.default_object_id = dc.object_id
     WHERE dc.parent_object_id = OBJECT_ID('CommessaFasi') AND c.name = 'RequiresTicket';
    IF @dfRequires IS NOT NULL EXEC('ALTER TABLE CommessaFasi DROP CONSTRAINT ' + @dfRequires);
    ALTER TABLE CommessaFasi DROP COLUMN RequiresTicket;
END

IF OBJECT_ID('CommessaFasi') IS NOT NULL AND COL_LENGTH('CommessaFasi','AutoCreateTicketOnTake') IS NOT NULL
BEGIN
    DECLARE @dfAutoCreate sysname;
    SELECT @dfAutoCreate = dc.name
      FROM sys.default_constraints dc
      JOIN sys.columns c ON c.default_object_id = dc.object_id
     WHERE dc.parent_object_id = OBJECT_ID('CommessaFasi') AND c.name = 'AutoCreateTicketOnTake';
    IF @dfAutoCreate IS NOT NULL EXEC('ALTER TABLE CommessaFasi DROP CONSTRAINT ' + @dfAutoCreate);
    ALTER TABLE CommessaFasi DROP COLUMN AutoCreateTicketOnTake;
END

IF OBJECT_ID('CommessaFasi') IS NOT NULL AND COL_LENGTH('CommessaFasi','CompletionMode') IS NOT NULL
BEGIN
    DECLARE @dfCompletion sysname;
    SELECT @dfCompletion = dc.name
      FROM sys.default_constraints dc
      JOIN sys.columns c ON c.default_object_id = dc.object_id
     WHERE dc.parent_object_id = OBJECT_ID('CommessaFasi') AND c.name = 'CompletionMode';
    IF @dfCompletion IS NOT NULL EXEC('ALTER TABLE CommessaFasi DROP CONSTRAINT ' + @dfCompletion);
    ALTER TABLE CommessaFasi DROP COLUMN CompletionMode;
END
");
        }
    }
}
