using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Tre tabelle di chiavi diventano una.
    /// <para>
    /// <c>MachineParameterApiKeys</c>, <c>ExternalTicketApiKeys</c> e <c>FieldApiKeys</c> avevano
    /// gli stessi campi e - verificato prima di scrivere questa migration - la stessa identica
    /// generazione: sigla + 32 byte casuali in base64, impronta SHA-256 in esadecimale, prefisso di
    /// 12 caratteri. Le impronte si copiano quindi tali e quali e <b>nessuna chiave gia'
    /// distribuita smette di funzionare</b>: era la condizione senza la quale questa unificazione
    /// non si sarebbe potuta fare.
    /// </para>
    /// <para>
    /// Cio' che le distingueva davvero - a chi sono intestate - diventa la colonna <c>Scope</c>,
    /// che entra anche nella verifica: una chiave vale nel suo ambito e in nessun altro.
    /// </para>
    /// <para>
    /// Scritta a mano perche' su questo SDK "dotnet ef migrations add" non funziona; istruzioni
    /// idempotenti, un batch per istruzione (le colonne appena create non sono usabili nello
    /// stesso blocco).
    /// </para>
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260805210000_UnifyApiKeys")]
    public partial class UnifyApiKeys : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.ApiKeys', 'U') IS NULL
CREATE TABLE [dbo].[ApiKeys] (
    [Id]         int IDENTITY(1,1) NOT NULL,
    [Scope]      int               NOT NULL,
    [Name]       nvarchar(150)     NOT NULL,
    [KeyHash]    nvarchar(64)      NOT NULL,
    [KeyPrefix]  nvarchar(20)      NULL,
    [Permission] int               NOT NULL CONSTRAINT [DF_ApiKeys_Permission] DEFAULT 2,
    [IdCompany]  int               NULL,
    [IdUser]     nvarchar(450)     NULL,
    [IsActive]   bit               NOT NULL CONSTRAINT [DF_ApiKeys_IsActive] DEFAULT 1,
    [CreatedAt]  datetime2         NOT NULL CONSTRAINT [DF_ApiKeys_CreatedAt] DEFAULT SYSUTCDATETIME(),
    [ExpiresAt]  datetime2         NULL,
    [LastUsedAt] datetime2         NULL,
    [Notes]      nvarchar(500)     NULL,
    CONSTRAINT [PK_ApiKeys] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ApiKeys_Companies_IdCompany]
        FOREIGN KEY ([IdCompany]) REFERENCES [dbo].[Companies] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ApiKeys_AspNetUsers_IdUser]
        FOREIGN KEY ([IdUser]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE NO ACTION
);
");

            // Le chiavi del backup: nessun intestatario, il permesso conta.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.MachineParameterApiKeys', 'U') IS NOT NULL
    INSERT INTO [dbo].[ApiKeys] ([Scope], [Name], [KeyHash], [KeyPrefix], [Permission], [IsActive], [CreatedAt], [ExpiresAt], [LastUsedAt], [Notes])
    SELECT 1, ISNULL(k.[Name], 'Chiave backup'), k.[KeyHash], k.[KeyPrefix], k.[Permission], k.[IsActive], k.[CreatedAt], k.[ExpiresAt], k.[LastUsedAt], k.[Notes]
    FROM [dbo].[MachineParameterApiKeys] k
    WHERE NOT EXISTS (SELECT 1 FROM [dbo].[ApiKeys] a WHERE a.[KeyHash] = k.[KeyHash]);
");

            // I ticket esterni: intestate a un'azienda.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.ExternalTicketApiKeys', 'U') IS NOT NULL
    INSERT INTO [dbo].[ApiKeys] ([Scope], [Name], [KeyHash], [KeyPrefix], [Permission], [IdCompany], [IsActive], [CreatedAt], [ExpiresAt], [LastUsedAt], [Notes])
    SELECT 2, k.[Name], k.[KeyHash], k.[KeyPrefix], 2, k.[IdCompany], k.[IsActive], k.[CreatedAt], k.[ExpiresAt], k.[LastUsedAt], k.[Notes]
    FROM [dbo].[ExternalTicketApiKeys] k
    WHERE NOT EXISTS (SELECT 1 FROM [dbo].[ApiKeys] a WHERE a.[KeyHash] = k.[KeyHash]);
");

            // L'app fiera: intestate a una persona.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.FieldApiKeys', 'U') IS NOT NULL
    INSERT INTO [dbo].[ApiKeys] ([Scope], [Name], [KeyHash], [KeyPrefix], [Permission], [IdUser], [IsActive], [CreatedAt], [ExpiresAt], [LastUsedAt], [Notes])
    SELECT 3, k.[Name], k.[KeyHash], k.[KeyPrefix], 2, k.[IdUser], k.[IsActive], k.[CreatedAt], k.[ExpiresAt], k.[LastUsedAt], k.[Notes]
    FROM [dbo].[FieldApiKeys] k
    WHERE NOT EXISTS (SELECT 1 FROM [dbo].[ApiKeys] a WHERE a.[KeyHash] = k.[KeyHash]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ApiKeys_KeyHash' AND object_id = OBJECT_ID('dbo.ApiKeys'))
    CREATE UNIQUE INDEX [IX_ApiKeys_KeyHash] ON [dbo].[ApiKeys] ([KeyHash]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ApiKeys_Scope' AND object_id = OBJECT_ID('dbo.ApiKeys'))
    CREATE INDEX [IX_ApiKeys_Scope] ON [dbo].[ApiKeys] ([Scope]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ApiKeys_IdCompany' AND object_id = OBJECT_ID('dbo.ApiKeys'))
    CREATE INDEX [IX_ApiKeys_IdCompany] ON [dbo].[ApiKeys] ([IdCompany]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ApiKeys_IdUser' AND object_id = OBJECT_ID('dbo.ApiKeys'))
    CREATE INDEX [IX_ApiKeys_IdUser] ON [dbo].[ApiKeys] ([IdUser]);
");

            // Le vecchie tabelle si rimuovono solo ADESSO, a copia riuscita e indici in piedi.
            migrationBuilder.Sql(@"IF OBJECT_ID('dbo.MachineParameterApiKeys', 'U') IS NOT NULL DROP TABLE [dbo].[MachineParameterApiKeys];");
            migrationBuilder.Sql(@"IF OBJECT_ID('dbo.ExternalTicketApiKeys', 'U') IS NOT NULL DROP TABLE [dbo].[ExternalTicketApiKeys];");
            migrationBuilder.Sql(@"IF OBJECT_ID('dbo.FieldApiKeys', 'U') IS NOT NULL DROP TABLE [dbo].[FieldApiKeys];");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Si ricostruiscono le tre tabelle e si riportano indietro le righe per ambito: le
            // impronte sono le stesse, quindi anche tornando indietro le chiavi restano valide.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.MachineParameterApiKeys', 'U') IS NULL
CREATE TABLE [dbo].[MachineParameterApiKeys] (
    [Id]         int IDENTITY(1,1) NOT NULL,
    [Name]       nvarchar(max)     NULL,
    [KeyHash]    nvarchar(max)     NOT NULL,
    [KeyPrefix]  nvarchar(max)     NULL,
    [Permission] int               NOT NULL,
    [IsActive]   bit               NOT NULL,
    [CreatedAt]  datetime2         NOT NULL,
    [ExpiresAt]  datetime2         NULL,
    [LastUsedAt] datetime2         NULL,
    [Notes]      nvarchar(max)     NULL,
    CONSTRAINT [PK_MachineParameterApiKeys] PRIMARY KEY ([Id])
);
");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.ExternalTicketApiKeys', 'U') IS NULL
CREATE TABLE [dbo].[ExternalTicketApiKeys] (
    [Id]         int IDENTITY(1,1) NOT NULL,
    [Name]       nvarchar(150)     NOT NULL,
    [KeyHash]    nvarchar(64)      NOT NULL,
    [KeyPrefix]  nvarchar(20)      NULL,
    [IdCompany]  int               NOT NULL,
    [IsActive]   bit               NOT NULL,
    [CreatedAt]  datetime2         NOT NULL,
    [ExpiresAt]  datetime2         NULL,
    [LastUsedAt] datetime2         NULL,
    [Notes]      nvarchar(500)     NULL,
    CONSTRAINT [PK_ExternalTicketApiKeys] PRIMARY KEY ([Id])
);
");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.FieldApiKeys', 'U') IS NULL
CREATE TABLE [dbo].[FieldApiKeys] (
    [Id]         int IDENTITY(1,1) NOT NULL,
    [Name]       nvarchar(150)     NOT NULL,
    [KeyHash]    nvarchar(64)      NOT NULL,
    [KeyPrefix]  nvarchar(20)      NULL,
    [IdUser]     nvarchar(450)     NOT NULL,
    [IsActive]   bit               NOT NULL,
    [CreatedAt]  datetime2         NOT NULL,
    [ExpiresAt]  datetime2         NULL,
    [LastUsedAt] datetime2         NULL,
    [Notes]      nvarchar(500)     NULL,
    CONSTRAINT [PK_FieldApiKeys] PRIMARY KEY ([Id])
);
");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.ApiKeys', 'U') IS NOT NULL
BEGIN
    INSERT INTO [dbo].[MachineParameterApiKeys] ([Name], [KeyHash], [KeyPrefix], [Permission], [IsActive], [CreatedAt], [ExpiresAt], [LastUsedAt], [Notes])
    SELECT [Name], [KeyHash], [KeyPrefix], [Permission], [IsActive], [CreatedAt], [ExpiresAt], [LastUsedAt], [Notes]
    FROM [dbo].[ApiKeys] WHERE [Scope] = 1;

    INSERT INTO [dbo].[ExternalTicketApiKeys] ([Name], [KeyHash], [KeyPrefix], [IdCompany], [IsActive], [CreatedAt], [ExpiresAt], [LastUsedAt], [Notes])
    SELECT [Name], [KeyHash], [KeyPrefix], ISNULL([IdCompany], 0), [IsActive], [CreatedAt], [ExpiresAt], [LastUsedAt], [Notes]
    FROM [dbo].[ApiKeys] WHERE [Scope] = 2 AND [IdCompany] IS NOT NULL;

    INSERT INTO [dbo].[FieldApiKeys] ([Name], [KeyHash], [KeyPrefix], [IdUser], [IsActive], [CreatedAt], [ExpiresAt], [LastUsedAt], [Notes])
    SELECT [Name], [KeyHash], [KeyPrefix], [IdUser], [IsActive], [CreatedAt], [ExpiresAt], [LastUsedAt], [Notes]
    FROM [dbo].[ApiKeys] WHERE [Scope] = 3 AND [IdUser] IS NOT NULL;
END
");

            migrationBuilder.Sql(@"IF OBJECT_ID('dbo.ApiKeys', 'U') IS NOT NULL DROP TABLE [dbo].[ApiKeys];");
        }
    }
}
