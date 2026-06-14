using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    public partial class ReplaceMachineParametersWithBackups : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[MachineBackups]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[MachineBackups]
    (
        [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_MachineBackups] PRIMARY KEY,
        [OwnerType] int NOT NULL,
        [IdProduct] int NULL,
        [IdArticle] int NULL,
        [FileName] nvarchar(255) NOT NULL,
        [ContentType] nvarchar(150) NOT NULL,
        [Size] bigint NOT NULL,
        [Sha256] nvarchar(64) NOT NULL,
        [Version] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [Source] int NOT NULL,
        [Description] nvarchar(500) NULL,
        [ExternalReference] nvarchar(200) NULL,
        [CreatedBy] nvarchar(450) NULL,
        CONSTRAINT [CK_MachineBackups_Owner] CHECK
        (([OwnerType] = 1 AND [IdProduct] IS NOT NULL AND [IdArticle] IS NULL) OR
         ([OwnerType] = 2 AND [IdArticle] IS NOT NULL AND [IdProduct] IS NULL)),
        CONSTRAINT [FK_MachineBackups_Articles_IdArticle] FOREIGN KEY ([IdArticle]) REFERENCES [dbo].[Articles]([Id]),
        CONSTRAINT [FK_MachineBackups_Products_IdProduct] FOREIGN KEY ([IdProduct]) REFERENCES [dbo].[Products]([Id])
    );

    CREATE INDEX [IX_MachineBackups_IdArticle] ON [dbo].[MachineBackups]([IdArticle]);
    CREATE INDEX [IX_MachineBackups_IdProduct] ON [dbo].[MachineBackups]([IdProduct]);
    CREATE UNIQUE INDEX [IX_MachineBackups_OwnerType_IdArticle_Version]
        ON [dbo].[MachineBackups]([OwnerType], [IdArticle], [Version]) WHERE [IdArticle] IS NOT NULL;
    CREATE UNIQUE INDEX [IX_MachineBackups_OwnerType_IdProduct_Version]
        ON [dbo].[MachineBackups]([OwnerType], [IdProduct], [Version]) WHERE [IdProduct] IS NOT NULL;
END;

-- Preserve the metadata of the legacy article backups. They intentionally have no downloadable file.
IF OBJECT_ID(N'[dbo].[ArticleBackups]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM [dbo].[MachineBackups])
BEGIN
    INSERT INTO [dbo].[MachineBackups]
        ([OwnerType], [IdProduct], [IdArticle], [FileName], [ContentType], [Size], [Sha256],
         [Version], [CreatedAt], [Source], [Description], [ExternalReference], [CreatedBy])
    SELECT 2, NULL, [IdArticle], N'legacy-backup-without-file', N'application/octet-stream', 0, N'',
           ROW_NUMBER() OVER (PARTITION BY [IdArticle] ORDER BY [TimeStamp], [Id]),
           [TimeStamp], 1, [Description], NULL, NULL
    FROM [dbo].[ArticleBackups];
END;

DROP TABLE IF EXISTS [dbo].[ArticleMachineParameterSnapshotItems];
DROP TABLE IF EXISTS [dbo].[BackUpParameters];
DROP TABLE IF EXISTS [dbo].[MachineDeviceTemplateAxisParameters];
DROP TABLE IF EXISTS [dbo].[MachineDeviceTemplateIoPoints];
DROP TABLE IF EXISTS [dbo].[MachineDeviceTemplateParameters];
DROP TABLE IF EXISTS [dbo].[ProductMachineAxisParameters];
DROP TABLE IF EXISTS [dbo].[ProductMachineIoPoints];
DROP TABLE IF EXISTS [dbo].[ProductMachineParameters];
DROP TABLE IF EXISTS [dbo].[ArticleMachineParameterSnapshots];
DROP TABLE IF EXISTS [dbo].[ArticleBackups];
DROP TABLE IF EXISTS [dbo].[ProductParameters];
DROP TABLE IF EXISTS [dbo].[MachineDeviceTemplateAxes];
DROP TABLE IF EXISTS [dbo].[MachineDeviceTemplateBoards];
DROP TABLE IF EXISTS [dbo].[ProductMachineAxes];
DROP TABLE IF EXISTS [dbo].[ProductMachineBoards];
DROP TABLE IF EXISTS [dbo].[ProductMachineDevices];
DROP TABLE IF EXISTS [dbo].[MachineDeviceTemplates];
DROP TABLE IF EXISTS [dbo].[ProductMachineConfigurations];
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS [dbo].[MachineBackups];");
        }
    }
}
