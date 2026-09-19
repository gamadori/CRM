using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Assistenza remota delle macchine: la corrispondenza fra la matricola nel CRM e
    /// il dispositivo (GUID) registrato sul server di provisioning. Una riga per
    /// macchina, un GUID per pannello. Nessuna chiave VPN o password VNC qui: restano
    /// sul VPS.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260918160000_AddMachineRemoteSupport")]
    public partial class AddMachineRemoteSupport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.MachineRemoteSupports') IS NULL
CREATE TABLE [dbo].[MachineRemoteSupports] (
    [Id] int NOT NULL IDENTITY,
    [IdArticle] int NOT NULL,
    [DeviceId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_MachineRemoteSupports] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MachineRemoteSupports_Articles_IdArticle] FOREIGN KEY ([IdArticle]) REFERENCES [dbo].[Articles] ([Id]) ON DELETE CASCADE
);
");

            // Una sola registrazione per macchina e un GUID unico per pannello.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MachineRemoteSupports_IdArticle')
CREATE UNIQUE INDEX [IX_MachineRemoteSupports_IdArticle] ON [dbo].[MachineRemoteSupports] ([IdArticle]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MachineRemoteSupports_DeviceId')
CREATE UNIQUE INDEX [IX_MachineRemoteSupports_DeviceId] ON [dbo].[MachineRemoteSupports] ([DeviceId]);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF OBJECT_ID('dbo.MachineRemoteSupports') IS NOT NULL DROP TABLE [dbo].[MachineRemoteSupports];");
        }
    }
}
