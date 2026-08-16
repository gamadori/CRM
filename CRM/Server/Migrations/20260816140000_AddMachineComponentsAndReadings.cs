using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Le macchine raccontano come sono fatte e quanto lavorano.
    /// <para>
    /// Tre tabelle: i componenti elettronici con la versione che hanno adesso (una linea ha piu'
    /// PLC, piu' schede e piu' HMI, quindi non possono essere campi sulla macchina), lo storico dei
    /// cambi di versione, e una riga per macchina e per giorno con ore e produzione.
    /// </para>
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260816140000_AddMachineComponentsAndReadings")]
    public partial class AddMachineComponentsAndReadings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.MachineComponents') IS NULL
CREATE TABLE [dbo].[MachineComponents] (
    [Id] int NOT NULL IDENTITY,
    [IdArticle] int NOT NULL,
    [Code] nvarchar(80) NOT NULL,
    [Kind] int NOT NULL,
    [Name] nvarchar(150) NULL,
    [CurrentVersion] nvarchar(80) NULL,
    [SerialNumber] nvarchar(80) NULL,
    [FirstSeenAt] datetime2 NOT NULL,
    [LastSeenAt] datetime2 NOT NULL,
    [LastVersionChangeAt] datetime2 NULL,
    CONSTRAINT [PK_MachineComponents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MachineComponents_Articles_IdArticle] FOREIGN KEY ([IdArticle]) REFERENCES [dbo].[Articles] ([Id]) ON DELETE CASCADE
);
");

            // La posizione e' l'identita' del componente dentro la macchina: due PLC-1 sulla stessa
            // linea vorrebbero dire due storie di versioni per lo stesso punto.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MachineComponents_IdArticle_Code')
CREATE UNIQUE INDEX [IX_MachineComponents_IdArticle_Code] ON [dbo].[MachineComponents] ([IdArticle], [Code]);
");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.MachineComponentVersionChanges') IS NULL
CREATE TABLE [dbo].[MachineComponentVersionChanges] (
    [Id] int NOT NULL IDENTITY,
    [IdMachineComponent] int NOT NULL,
    [FromVersion] nvarchar(80) NULL,
    [ToVersion] nvarchar(80) NULL,
    [DetectedAt] datetime2 NOT NULL,
    [Source] nvarchar(120) NULL,
    CONSTRAINT [PK_MachineComponentVersionChanges] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MachineComponentVersionChanges_MachineComponents_IdMachineComponent] FOREIGN KEY ([IdMachineComponent]) REFERENCES [dbo].[MachineComponents] ([Id]) ON DELETE CASCADE
);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MachineComponentVersionChanges_IdMachineComponent_DetectedAt')
CREATE INDEX [IX_MachineComponentVersionChanges_IdMachineComponent_DetectedAt] ON [dbo].[MachineComponentVersionChanges] ([IdMachineComponent], [DetectedAt]);
");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.MachineDailyReadings') IS NULL
CREATE TABLE [dbo].[MachineDailyReadings] (
    [Id] int NOT NULL IDENTITY,
    [IdArticle] int NOT NULL,
    [Day] date NOT NULL,
    [TotalHours] decimal(18,2) NULL,
    [TotalPieces] bigint NULL,
    [Hours] decimal(18,2) NULL,
    [Pieces] bigint NULL,
    [CounterReset] bit NOT NULL,
    [ReceivedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_MachineDailyReadings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MachineDailyReadings_Articles_IdArticle] FOREIGN KEY ([IdArticle]) REFERENCES [dbo].[Articles] ([Id]) ON DELETE CASCADE
);
");

            // Una lettura per macchina e per giorno: due letture dello stesso giorno sarebbero la
            // stessa produzione contata due volte.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MachineDailyReadings_IdArticle_Day')
CREATE UNIQUE INDEX [IX_MachineDailyReadings_IdArticle_Day] ON [dbo].[MachineDailyReadings] ([IdArticle], [Day]);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF OBJECT_ID('dbo.MachineComponentVersionChanges') IS NOT NULL DROP TABLE [dbo].[MachineComponentVersionChanges];");
            migrationBuilder.Sql("IF OBJECT_ID('dbo.MachineComponents') IS NOT NULL DROP TABLE [dbo].[MachineComponents];");
            migrationBuilder.Sql("IF OBJECT_ID('dbo.MachineDailyReadings') IS NOT NULL DROP TABLE [dbo].[MachineDailyReadings];");
        }
    }
}
