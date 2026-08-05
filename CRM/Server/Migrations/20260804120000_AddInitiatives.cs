using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Iniziative: fiere, trasferte, campagne. Il contenitore che mancava per dare una casa a lead,
    /// attivita', note spese e documenti nati da una stessa occasione.
    /// <para>
    /// Tutte le chiavi verso Initiatives sono NULLABLE e ON DELETE NO ACTION: cancellare
    /// un'iniziativa non deve portarsi via cio' che ha prodotto. Lo sgancio lo fa il servizio prima
    /// di cancellare - stessa strada dell'origine attivita' su Deal/Quote, e per lo stesso motivo:
    /// la cascata su dati commerciali e' silenziosa e irreversibile.
    /// </para>
    /// <para>
    /// Scritta a mano perche' su questo SDK "dotnet ef migrations add" non funziona; le istruzioni
    /// sono idempotenti cosi' un'applicazione parziale non lascia il database a meta'. Ogni Sql() e'
    /// un batch a se': le colonne appena aggiunte non si possono usare nello stesso blocco.
    /// </para>
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260804120000_AddInitiatives")]
    public partial class AddInitiatives : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.Initiatives', 'U') IS NULL
CREATE TABLE [dbo].[Initiatives] (
    [Id]            int IDENTITY(1,1)   NOT NULL,
    [Name]          nvarchar(200)       NOT NULL,
    [Kind]          int                 NOT NULL,
    [State]         int                 NOT NULL,
    [Location]      nvarchar(200)       NULL,
    [DateFrom]      datetime2           NOT NULL,
    [DateTo]        datetime2           NOT NULL,
    [BudgetPlanned] decimal(18,2)       NULL,
    [Objective]     nvarchar(max)       NULL,
    [ClosingNotes]  nvarchar(max)       NULL,
    [IdOwner]       nvarchar(450)       NULL,
    [CreatedAt]     datetime2           NOT NULL,
    [ClosedAt]      datetime2           NULL,
    CONSTRAINT [PK_Initiatives] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Initiatives_AspNetUsers_IdOwner]
        FOREIGN KEY ([IdOwner]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE NO ACTION
);
");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.InitiativeParticipants', 'U') IS NULL
CREATE TABLE [dbo].[InitiativeParticipants] (
    [Id]            int IDENTITY(1,1)   NOT NULL,
    [IdInitiative]  int                 NOT NULL,
    [IdUser]        nvarchar(450)       NOT NULL,
    [AddedAt]       datetime2           NOT NULL,
    [AddedBy]       nvarchar(max)       NULL,
    CONSTRAINT [PK_InitiativeParticipants] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InitiativeParticipants_Initiatives_IdInitiative]
        FOREIGN KEY ([IdInitiative]) REFERENCES [dbo].[Initiatives] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_InitiativeParticipants_AspNetUsers_IdUser]
        FOREIGN KEY ([IdUser]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE NO ACTION
);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Initiatives_Kind_DateFrom' AND object_id = OBJECT_ID('dbo.Initiatives'))
    CREATE INDEX [IX_Initiatives_Kind_DateFrom] ON [dbo].[Initiatives] ([Kind], [DateFrom]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Initiatives_State' AND object_id = OBJECT_ID('dbo.Initiatives'))
    CREATE INDEX [IX_Initiatives_State] ON [dbo].[Initiatives] ([State]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Initiatives_IdOwner' AND object_id = OBJECT_ID('dbo.Initiatives'))
    CREATE INDEX [IX_Initiatives_IdOwner] ON [dbo].[Initiatives] ([IdOwner]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InitiativeParticipants_IdInitiative' AND object_id = OBJECT_ID('dbo.InitiativeParticipants'))
    CREATE INDEX [IX_InitiativeParticipants_IdInitiative] ON [dbo].[InitiativeParticipants] ([IdInitiative]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InitiativeParticipants_IdUser' AND object_id = OBJECT_ID('dbo.InitiativeParticipants'))
    CREATE INDEX [IX_InitiativeParticipants_IdUser] ON [dbo].[InitiativeParticipants] ([IdUser]);
");

            // ---- le quattro chiavi verso l'iniziativa ------------------------------------

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Activities', 'IdInitiative') IS NULL
    ALTER TABLE [dbo].[Activities] ADD [IdInitiative] int NULL;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.ExpenseReceipts', 'IdInitiative') IS NULL
    ALTER TABLE [dbo].[ExpenseReceipts] ADD [IdInitiative] int NULL;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Leads', 'IdInitiative') IS NULL
    ALTER TABLE [dbo].[Leads] ADD [IdInitiative] int NULL;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Deals', 'IdInitiative') IS NULL
    ALTER TABLE [dbo].[Deals] ADD [IdInitiative] int NULL;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Activities_IdInitiative' AND object_id = OBJECT_ID('dbo.Activities'))
    CREATE INDEX [IX_Activities_IdInitiative] ON [dbo].[Activities] ([IdInitiative]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExpenseReceipts_IdInitiative' AND object_id = OBJECT_ID('dbo.ExpenseReceipts'))
    CREATE INDEX [IX_ExpenseReceipts_IdInitiative] ON [dbo].[ExpenseReceipts] ([IdInitiative]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Leads_IdInitiative' AND object_id = OBJECT_ID('dbo.Leads'))
    CREATE INDEX [IX_Leads_IdInitiative] ON [dbo].[Leads] ([IdInitiative]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Deals_IdInitiative' AND object_id = OBJECT_ID('dbo.Deals'))
    CREATE INDEX [IX_Deals_IdInitiative] ON [dbo].[Deals] ([IdInitiative]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Activities_Initiatives_IdInitiative')
    ALTER TABLE [dbo].[Activities] ADD CONSTRAINT [FK_Activities_Initiatives_IdInitiative]
        FOREIGN KEY ([IdInitiative]) REFERENCES [dbo].[Initiatives] ([Id]) ON DELETE NO ACTION;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ExpenseReceipts_Initiatives_IdInitiative')
    ALTER TABLE [dbo].[ExpenseReceipts] ADD CONSTRAINT [FK_ExpenseReceipts_Initiatives_IdInitiative]
        FOREIGN KEY ([IdInitiative]) REFERENCES [dbo].[Initiatives] ([Id]) ON DELETE NO ACTION;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Leads_Initiatives_IdInitiative')
    ALTER TABLE [dbo].[Leads] ADD CONSTRAINT [FK_Leads_Initiatives_IdInitiative]
        FOREIGN KEY ([IdInitiative]) REFERENCES [dbo].[Initiatives] ([Id]) ON DELETE NO ACTION;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Deals_Initiatives_IdInitiative')
    ALTER TABLE [dbo].[Deals] ADD CONSTRAINT [FK_Deals_Initiatives_IdInitiative]
        FOREIGN KEY ([IdInitiative]) REFERENCES [dbo].[Initiatives] ([Id]) ON DELETE NO ACTION;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Activities_Initiatives_IdInitiative')
    ALTER TABLE [dbo].[Activities] DROP CONSTRAINT [FK_Activities_Initiatives_IdInitiative];
");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ExpenseReceipts_Initiatives_IdInitiative')
    ALTER TABLE [dbo].[ExpenseReceipts] DROP CONSTRAINT [FK_ExpenseReceipts_Initiatives_IdInitiative];
");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Leads_Initiatives_IdInitiative')
    ALTER TABLE [dbo].[Leads] DROP CONSTRAINT [FK_Leads_Initiatives_IdInitiative];
");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Deals_Initiatives_IdInitiative')
    ALTER TABLE [dbo].[Deals] DROP CONSTRAINT [FK_Deals_Initiatives_IdInitiative];
");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Activities_IdInitiative' AND object_id = OBJECT_ID('dbo.Activities'))
    DROP INDEX [IX_Activities_IdInitiative] ON [dbo].[Activities];
");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExpenseReceipts_IdInitiative' AND object_id = OBJECT_ID('dbo.ExpenseReceipts'))
    DROP INDEX [IX_ExpenseReceipts_IdInitiative] ON [dbo].[ExpenseReceipts];
");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Leads_IdInitiative' AND object_id = OBJECT_ID('dbo.Leads'))
    DROP INDEX [IX_Leads_IdInitiative] ON [dbo].[Leads];
");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Deals_IdInitiative' AND object_id = OBJECT_ID('dbo.Deals'))
    DROP INDEX [IX_Deals_IdInitiative] ON [dbo].[Deals];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Activities', 'IdInitiative') IS NOT NULL
    ALTER TABLE [dbo].[Activities] DROP COLUMN [IdInitiative];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.ExpenseReceipts', 'IdInitiative') IS NOT NULL
    ALTER TABLE [dbo].[ExpenseReceipts] DROP COLUMN [IdInitiative];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Leads', 'IdInitiative') IS NOT NULL
    ALTER TABLE [dbo].[Leads] DROP COLUMN [IdInitiative];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Deals', 'IdInitiative') IS NOT NULL
    ALTER TABLE [dbo].[Deals] DROP COLUMN [IdInitiative];
");
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.InitiativeParticipants', 'U') IS NOT NULL DROP TABLE [dbo].[InitiativeParticipants];
");
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.Initiatives', 'U') IS NOT NULL DROP TABLE [dbo].[Initiatives];
");
        }
    }
}
