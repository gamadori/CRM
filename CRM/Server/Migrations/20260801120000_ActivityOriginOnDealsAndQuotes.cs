using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Da quale attivita' nascono opportunita' e preventivi.
    /// <para>
    /// Il vincolo e' NO ACTION di proposito: cancellando l'attivita' non si devono perdere
    /// l'opportunita' o il preventivo che ne sono nati. Lo sgancio lo fa il servizio prima di
    /// cancellare - stessa strada delle note spese sul ticket, e per lo stesso motivo: la
    /// cancellazione a cascata di dati commerciali e' silenziosa e irreversibile.
    /// </para>
    /// <para>
    /// Scritta a mano perche' su questo SDK "dotnet ef migrations add" non funziona; le istruzioni
    /// sono idempotenti cosi' un'applicazione parziale non lascia il database a meta'.
    /// </para>
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260801120000_ActivityOriginOnDealsAndQuotes")]
    public partial class ActivityOriginOnDealsAndQuotes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Deals', 'IdActivityOrigin') IS NULL
    ALTER TABLE [dbo].[Deals] ADD [IdActivityOrigin] int NULL;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Quotes', 'IdActivityOrigin') IS NULL
    ALTER TABLE [dbo].[Quotes] ADD [IdActivityOrigin] int NULL;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Deals_IdActivityOrigin' AND object_id = OBJECT_ID('dbo.Deals'))
    CREATE INDEX [IX_Deals_IdActivityOrigin] ON [dbo].[Deals] ([IdActivityOrigin]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Quotes_IdActivityOrigin' AND object_id = OBJECT_ID('dbo.Quotes'))
    CREATE INDEX [IX_Quotes_IdActivityOrigin] ON [dbo].[Quotes] ([IdActivityOrigin]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Deals_Activities_IdActivityOrigin')
    ALTER TABLE [dbo].[Deals] ADD CONSTRAINT [FK_Deals_Activities_IdActivityOrigin]
        FOREIGN KEY ([IdActivityOrigin]) REFERENCES [dbo].[Activities] ([Id]) ON DELETE NO ACTION;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Quotes_Activities_IdActivityOrigin')
    ALTER TABLE [dbo].[Quotes] ADD CONSTRAINT [FK_Quotes_Activities_IdActivityOrigin]
        FOREIGN KEY ([IdActivityOrigin]) REFERENCES [dbo].[Activities] ([Id]) ON DELETE NO ACTION;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Deals_Activities_IdActivityOrigin')
    ALTER TABLE [dbo].[Deals] DROP CONSTRAINT [FK_Deals_Activities_IdActivityOrigin];
");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Quotes_Activities_IdActivityOrigin')
    ALTER TABLE [dbo].[Quotes] DROP CONSTRAINT [FK_Quotes_Activities_IdActivityOrigin];
");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Deals_IdActivityOrigin' AND object_id = OBJECT_ID('dbo.Deals'))
    DROP INDEX [IX_Deals_IdActivityOrigin] ON [dbo].[Deals];
");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Quotes_IdActivityOrigin' AND object_id = OBJECT_ID('dbo.Quotes'))
    DROP INDEX [IX_Quotes_IdActivityOrigin] ON [dbo].[Quotes];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Deals', 'IdActivityOrigin') IS NOT NULL
    ALTER TABLE [dbo].[Deals] DROP COLUMN [IdActivityOrigin];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Quotes', 'IdActivityOrigin') IS NOT NULL
    ALTER TABLE [dbo].[Quotes] DROP COLUMN [IdActivityOrigin];
");
        }
    }
}
