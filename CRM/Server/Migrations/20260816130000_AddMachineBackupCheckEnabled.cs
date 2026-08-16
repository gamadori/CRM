using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// L'interruttore del controllo sui backup, e piu' destinatari per il riepilogo.
    /// <para>
    /// Sta in una migration a se' e non dentro
    /// <c>20260816120000_AddMachineBackupRetentionAndSilence</c> perche' quella era gia' stata
    /// applicata: una migration registrata non viene rieseguita, quindi le colonne aggiunte al suo
    /// file dopo il primo avvio non sarebbero mai arrivate al database. E' esattamente cosi' che il
    /// server si e' fermato su "nome di colonna non valido".
    /// </para>
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260816130000_AddMachineBackupCheckEnabled")]
    public partial class AddMachineBackupCheckEnabled : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Spento: accendere una sorveglianza che manda email deve restare una decisione.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'MachineBackupCheckEnabled' AND object_id = OBJECT_ID('dbo.GlobalSettings'))
    ALTER TABLE [dbo].[GlobalSettings] ADD [MachineBackupCheckEnabled] bit NOT NULL CONSTRAINT [DF_GlobalSettings_MachineBackupCheckEnabled] DEFAULT 0;
");

            // Piu' indirizzi separati da punto e virgola non stanno in 200 caratteri.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'MachineBackupSilenceEmail' AND object_id = OBJECT_ID('dbo.GlobalSettings'))
    ALTER TABLE [dbo].[GlobalSettings] ALTER COLUMN [MachineBackupSilenceEmail] nvarchar(500) NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_GlobalSettings_MachineBackupCheckEnabled')
    ALTER TABLE [dbo].[GlobalSettings] DROP CONSTRAINT [DF_GlobalSettings_MachineBackupCheckEnabled];
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'MachineBackupCheckEnabled' AND object_id = OBJECT_ID('dbo.GlobalSettings'))
    ALTER TABLE [dbo].[GlobalSettings] DROP COLUMN [MachineBackupCheckEnabled];
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'MachineBackupSilenceEmail' AND object_id = OBJECT_ID('dbo.GlobalSettings'))
    ALTER TABLE [dbo].[GlobalSettings] ALTER COLUMN [MachineBackupSilenceEmail] nvarchar(200) NULL;
");
        }
    }
}
