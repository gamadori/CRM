using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Una regola di automazione puo' essere ristretta a una singola iniziativa.
    /// <para>
    /// La colonna e' NULLABLE e nasce vuota: tutte le regole gia' esistenti continuano a valere per
    /// qualsiasi record, che e' esattamente il comportamento che avevano ieri. Un default diverso
    /// avrebbe cambiato in silenzio il significato di regole gia' in produzione.
    /// </para>
    /// <para>
    /// FK NO ACTION come ogni altra chiave verso Initiatives: cancellare una fiera non deve
    /// portarsi via nulla. Lo sgancio lo fa InitiativesService, che per le automazioni sgancia E
    /// disattiva - una regola che perde il vincolo diventerebbe altrimenti "qualsiasi evento".
    /// </para>
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260811120000_AddWorkflowAutomationInitiative")]
    public partial class AddWorkflowAutomationInitiative : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'IdInitiative' AND object_id = OBJECT_ID('dbo.WorkflowAutomations'))
    ALTER TABLE [dbo].[WorkflowAutomations] ADD [IdInitiative] int NULL;
");

            // Batch a se': la colonna appena aggiunta non e' visibile nello stesso lotto.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WorkflowAutomations_IdInitiative' AND object_id = OBJECT_ID('dbo.WorkflowAutomations'))
    CREATE INDEX [IX_WorkflowAutomations_IdInitiative] ON [dbo].[WorkflowAutomations] ([IdInitiative]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_WorkflowAutomations_Initiatives_IdInitiative')
    ALTER TABLE [dbo].[WorkflowAutomations] ADD CONSTRAINT [FK_WorkflowAutomations_Initiatives_IdInitiative]
        FOREIGN KEY ([IdInitiative]) REFERENCES [dbo].[Initiatives] ([Id]) ON DELETE NO ACTION;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_WorkflowAutomations_Initiatives_IdInitiative')
    ALTER TABLE [dbo].[WorkflowAutomations] DROP CONSTRAINT [FK_WorkflowAutomations_Initiatives_IdInitiative];
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WorkflowAutomations_IdInitiative' AND object_id = OBJECT_ID('dbo.WorkflowAutomations'))
    DROP INDEX [IX_WorkflowAutomations_IdInitiative] ON [dbo].[WorkflowAutomations];
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'IdInitiative' AND object_id = OBJECT_ID('dbo.WorkflowAutomations'))
    ALTER TABLE [dbo].[WorkflowAutomations] DROP COLUMN [IdInitiative];
");
        }
    }
}
