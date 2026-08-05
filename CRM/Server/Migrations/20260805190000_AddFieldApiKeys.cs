using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Chiavi dell'app di cattura biglietti e identificativo con cui l'app evita i doppioni.
    /// <para>
    /// La chiave e' intestata a una persona (cascata sull'utente: rimosso l'utente, la sua chiave
    /// non deve restare valida) mentre il lead sopravvive sempre - l'identificativo dell'app e'
    /// solo una colonna in piu' sui Leads.
    /// </para>
    /// <para>
    /// Scritta a mano perche' su questo SDK "dotnet ef migrations add" non funziona; istruzioni
    /// idempotenti, un batch per istruzione.
    /// </para>
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260805190000_AddFieldApiKeys")]
    public partial class AddFieldApiKeys : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.FieldApiKeys', 'U') IS NULL
CREATE TABLE [dbo].[FieldApiKeys] (
    [Id]         int IDENTITY(1,1) NOT NULL,
    [Name]       nvarchar(150)     NOT NULL,
    [KeyHash]    nvarchar(64)      NOT NULL,
    [KeyPrefix]  nvarchar(20)      NULL,
    [IdUser]     nvarchar(450)     NOT NULL,
    [IsActive]   bit               NOT NULL CONSTRAINT [DF_FieldApiKeys_IsActive] DEFAULT 1,
    [CreatedAt]  datetime2         NOT NULL CONSTRAINT [DF_FieldApiKeys_CreatedAt] DEFAULT SYSUTCDATETIME(),
    [ExpiresAt]  datetime2         NULL,
    [LastUsedAt] datetime2         NULL,
    [Notes]      nvarchar(500)     NULL,
    CONSTRAINT [PK_FieldApiKeys] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FieldApiKeys_AspNetUsers_IdUser]
        FOREIGN KEY ([IdUser]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FieldApiKeys_KeyHash' AND object_id = OBJECT_ID('dbo.FieldApiKeys'))
    CREATE UNIQUE INDEX [IX_FieldApiKeys_KeyHash] ON [dbo].[FieldApiKeys] ([KeyHash]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FieldApiKeys_IdUser' AND object_id = OBJECT_ID('dbo.FieldApiKeys'))
    CREATE INDEX [IX_FieldApiKeys_IdUser] ON [dbo].[FieldApiKeys] ([IdUser]);
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Leads', 'FieldClientId') IS NULL
    ALTER TABLE [dbo].[Leads] ADD [FieldClientId] nvarchar(64) NULL;
");

            // Indice FILTRATO: la colonna e' nulla su tutti i lead che non vengono dall'app, e un
            // unico normale accetterebbe un solo NULL in tutta la tabella.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Leads_FieldClientId' AND object_id = OBJECT_ID('dbo.Leads'))
    CREATE UNIQUE INDEX [IX_Leads_FieldClientId] ON [dbo].[Leads] ([FieldClientId]) WHERE [FieldClientId] IS NOT NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Leads_FieldClientId' AND object_id = OBJECT_ID('dbo.Leads'))
    DROP INDEX [IX_Leads_FieldClientId] ON [dbo].[Leads];
");
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Leads', 'FieldClientId') IS NOT NULL
    ALTER TABLE [dbo].[Leads] DROP COLUMN [FieldClientId];
");
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.FieldApiKeys', 'U') IS NOT NULL DROP TABLE [dbo].[FieldApiKeys];
");
        }
    }
}
