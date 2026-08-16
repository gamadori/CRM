using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Conservazione dei backup macchina e sorveglianza sul silenzio.
    /// <para>
    /// Le impostazioni nascono a zero, cioe' spente: nessun backup viene cancellato e nessun avviso
    /// parte finche' non le configura una persona. E' voluto - il giorno che si accende la
    /// cancellazione automatica di file dei clienti, deve essere una decisione, non l'effetto di un
    /// aggiornamento.
    /// </para>
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260816120000_AddMachineBackupRetentionAndSilence")]
    public partial class AddMachineBackupRetentionAndSilence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'MachineBackupKeepVersions' AND object_id = OBJECT_ID('dbo.GlobalSettings'))
    ALTER TABLE [dbo].[GlobalSettings] ADD [MachineBackupKeepVersions] int NOT NULL CONSTRAINT [DF_GlobalSettings_MachineBackupKeepVersions] DEFAULT 0;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'MachineBackupSilenceDays' AND object_id = OBJECT_ID('dbo.GlobalSettings'))
    ALTER TABLE [dbo].[GlobalSettings] ADD [MachineBackupSilenceDays] int NOT NULL CONSTRAINT [DF_GlobalSettings_MachineBackupSilenceDays] DEFAULT 0;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'MachineBackupSilenceEmail' AND object_id = OBJECT_ID('dbo.GlobalSettings'))
    ALTER TABLE [dbo].[GlobalSettings] ADD [MachineBackupSilenceEmail] nvarchar(200) NULL;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'MachineBackupSilenceLastSentOn' AND object_id = OBJECT_ID('dbo.GlobalSettings'))
    ALTER TABLE [dbo].[GlobalSettings] ADD [MachineBackupSilenceLastSentOn] datetime2 NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_GlobalSettings_MachineBackupKeepVersions')
    ALTER TABLE [dbo].[GlobalSettings] DROP CONSTRAINT [DF_GlobalSettings_MachineBackupKeepVersions];
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'MachineBackupKeepVersions' AND object_id = OBJECT_ID('dbo.GlobalSettings'))
    ALTER TABLE [dbo].[GlobalSettings] DROP COLUMN [MachineBackupKeepVersions];
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_GlobalSettings_MachineBackupSilenceDays')
    ALTER TABLE [dbo].[GlobalSettings] DROP CONSTRAINT [DF_GlobalSettings_MachineBackupSilenceDays];
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'MachineBackupSilenceDays' AND object_id = OBJECT_ID('dbo.GlobalSettings'))
    ALTER TABLE [dbo].[GlobalSettings] DROP COLUMN [MachineBackupSilenceDays];
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'MachineBackupSilenceEmail' AND object_id = OBJECT_ID('dbo.GlobalSettings'))
    ALTER TABLE [dbo].[GlobalSettings] DROP COLUMN [MachineBackupSilenceEmail];
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'MachineBackupSilenceLastSentOn' AND object_id = OBJECT_ID('dbo.GlobalSettings'))
    ALTER TABLE [dbo].[GlobalSettings] DROP COLUMN [MachineBackupSilenceLastSentOn];
");
        }
    }
}
