using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Porta le iniziative da semplice contenitore consuntivo a pianificazione operativa:
    /// membri con ruolo e presenze/turni visibili in agenda.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260805110000_RefactorInitiativePlanning")]
    public partial class RefactorInitiativePlanning : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.InitiativeMembers', 'U') IS NULL
CREATE TABLE [dbo].[InitiativeMembers] (
    [Id]           int IDENTITY(1,1) NOT NULL,
    [IdInitiative] int NOT NULL,
    [IdUser]       nvarchar(450) NOT NULL,
    [Role]         int NOT NULL CONSTRAINT [DF_InitiativeMembers_Role] DEFAULT 1,
    [Notes]        nvarchar(500) NULL,
    [AddedAt]      datetime2 NOT NULL CONSTRAINT [DF_InitiativeMembers_AddedAt] DEFAULT SYSUTCDATETIME(),
    [AddedBy]      nvarchar(max) NULL,
    CONSTRAINT [PK_InitiativeMembers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InitiativeMembers_Initiatives_IdInitiative]
        FOREIGN KEY ([IdInitiative]) REFERENCES [dbo].[Initiatives] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_InitiativeMembers_AspNetUsers_IdUser]
        FOREIGN KEY ([IdUser]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE NO ACTION
);

IF OBJECT_ID('dbo.InitiativeParticipants', 'U') IS NOT NULL
BEGIN
    INSERT INTO [dbo].[InitiativeMembers] ([IdInitiative], [IdUser], [Role], [AddedAt], [AddedBy])
    SELECT p.[IdInitiative], p.[IdUser], 1, p.[AddedAt], p.[AddedBy]
    FROM [dbo].[InitiativeParticipants] p
    WHERE NOT EXISTS (
        SELECT 1
        FROM [dbo].[InitiativeMembers] m
        WHERE m.[IdInitiative] = p.[IdInitiative] AND m.[IdUser] = p.[IdUser]
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InitiativeMembers_IdInitiative_IdUser' AND object_id = OBJECT_ID('dbo.InitiativeMembers'))
    CREATE UNIQUE INDEX [IX_InitiativeMembers_IdInitiative_IdUser] ON [dbo].[InitiativeMembers] ([IdInitiative], [IdUser]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InitiativeMembers_IdInitiative' AND object_id = OBJECT_ID('dbo.InitiativeMembers'))
    CREATE INDEX [IX_InitiativeMembers_IdInitiative] ON [dbo].[InitiativeMembers] ([IdInitiative]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InitiativeMembers_IdUser' AND object_id = OBJECT_ID('dbo.InitiativeMembers'))
    CREATE INDEX [IX_InitiativeMembers_IdUser] ON [dbo].[InitiativeMembers] ([IdUser]);

IF OBJECT_ID('dbo.InitiativeSchedules', 'U') IS NULL
CREATE TABLE [dbo].[InitiativeSchedules] (
    [Id]           int IDENTITY(1,1) NOT NULL,
    [IdInitiative] int NOT NULL,
    [IdUser]       nvarchar(450) NOT NULL,
    [Start]        datetime2 NOT NULL,
    [End]          datetime2 NOT NULL,
    [Type]         int NOT NULL CONSTRAINT [DF_InitiativeSchedules_Type] DEFAULT 0,
    [Location]     nvarchar(200) NULL,
    [Notes]        nvarchar(1000) NULL,
    [CreatedAt]    datetime2 NOT NULL CONSTRAINT [DF_InitiativeSchedules_CreatedAt] DEFAULT SYSUTCDATETIME(),
    [CreatedBy]    nvarchar(max) NULL,
    CONSTRAINT [PK_InitiativeSchedules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InitiativeSchedules_Initiatives_IdInitiative]
        FOREIGN KEY ([IdInitiative]) REFERENCES [dbo].[Initiatives] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_InitiativeSchedules_AspNetUsers_IdUser]
        FOREIGN KEY ([IdUser]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [CK_InitiativeSchedules_EndAfterStart] CHECK ([End] > [Start])
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InitiativeSchedules_IdInitiative' AND object_id = OBJECT_ID('dbo.InitiativeSchedules'))
    CREATE INDEX [IX_InitiativeSchedules_IdInitiative] ON [dbo].[InitiativeSchedules] ([IdInitiative]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InitiativeSchedules_IdUser' AND object_id = OBJECT_ID('dbo.InitiativeSchedules'))
    CREATE INDEX [IX_InitiativeSchedules_IdUser] ON [dbo].[InitiativeSchedules] ([IdUser]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InitiativeSchedules_Start_End' AND object_id = OBJECT_ID('dbo.InitiativeSchedules'))
    CREATE INDEX [IX_InitiativeSchedules_Start_End] ON [dbo].[InitiativeSchedules] ([Start], [End]);

IF OBJECT_ID('dbo.InitiativeParticipants', 'U') IS NOT NULL
    DROP TABLE [dbo].[InitiativeParticipants];
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.InitiativeParticipants', 'U') IS NULL
CREATE TABLE [dbo].[InitiativeParticipants] (
    [Id]           int IDENTITY(1,1) NOT NULL,
    [IdInitiative] int NOT NULL,
    [IdUser]       nvarchar(450) NOT NULL,
    [AddedAt]      datetime2 NOT NULL,
    [AddedBy]      nvarchar(max) NULL,
    CONSTRAINT [PK_InitiativeParticipants] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InitiativeParticipants_Initiatives_IdInitiative]
        FOREIGN KEY ([IdInitiative]) REFERENCES [dbo].[Initiatives] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_InitiativeParticipants_AspNetUsers_IdUser]
        FOREIGN KEY ([IdUser]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE NO ACTION
);

IF OBJECT_ID('dbo.InitiativeMembers', 'U') IS NOT NULL
BEGIN
    INSERT INTO [dbo].[InitiativeParticipants] ([IdInitiative], [IdUser], [AddedAt], [AddedBy])
    SELECT [IdInitiative], [IdUser], [AddedAt], [AddedBy]
    FROM [dbo].[InitiativeMembers];
END

IF OBJECT_ID('dbo.InitiativeSchedules', 'U') IS NOT NULL DROP TABLE [dbo].[InitiativeSchedules];
IF OBJECT_ID('dbo.InitiativeMembers', 'U') IS NOT NULL DROP TABLE [dbo].[InitiativeMembers];
");
        }
    }
}
