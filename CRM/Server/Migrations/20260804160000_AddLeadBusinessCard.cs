using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Foto del biglietto da visita sul lead.
    /// <para>
    /// NO ACTION come le altre: cancellare il file non deve portarsi via il contatto. Il contrario
    /// e' invece accettabile e voluto - un lead cancellato lascia il file orfano, che e' un costo
    /// di spazio, non una perdita di dati.
    /// </para>
    /// <para>
    /// Scritta a mano perche' su questo SDK "dotnet ef migrations add" non funziona; istruzioni
    /// idempotenti, un batch per istruzione.
    /// </para>
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260804160000_AddLeadBusinessCard")]
    public partial class AddLeadBusinessCard : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Leads', 'IdBusinessCard') IS NULL
    ALTER TABLE [dbo].[Leads] ADD [IdBusinessCard] int NULL;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Leads_IdBusinessCard' AND object_id = OBJECT_ID('dbo.Leads'))
    CREATE INDEX [IX_Leads_IdBusinessCard] ON [dbo].[Leads] ([IdBusinessCard]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Leads_AttachmentFiles_IdBusinessCard')
    ALTER TABLE [dbo].[Leads] ADD CONSTRAINT [FK_Leads_AttachmentFiles_IdBusinessCard]
        FOREIGN KEY ([IdBusinessCard]) REFERENCES [dbo].[AttachmentFiles] ([Id]) ON DELETE NO ACTION;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Leads_AttachmentFiles_IdBusinessCard')
    ALTER TABLE [dbo].[Leads] DROP CONSTRAINT [FK_Leads_AttachmentFiles_IdBusinessCard];
");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Leads_IdBusinessCard' AND object_id = OBJECT_ID('dbo.Leads'))
    DROP INDEX [IX_Leads_IdBusinessCard] ON [dbo].[Leads];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Leads', 'IdBusinessCard') IS NOT NULL
    ALTER TABLE [dbo].[Leads] DROP COLUMN [IdBusinessCard];
");
        }
    }
}
