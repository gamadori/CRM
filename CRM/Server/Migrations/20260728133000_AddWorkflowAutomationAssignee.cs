using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260728133000_AddWorkflowAutomationAssignee")]
    public partial class AddWorkflowAutomationAssignee : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('WorkflowAutomations','IdAssignee') IS NULL
    ALTER TABLE WorkflowAutomations ADD IdAssignee nvarchar(450) NULL;

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_WorkflowAutomations_IdAssignee' AND object_id=OBJECT_ID('WorkflowAutomations'))
    CREATE INDEX IX_WorkflowAutomations_IdAssignee ON WorkflowAutomations(IdAssignee);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_WorkflowAutomations_IdAssignee' AND object_id=OBJECT_ID('WorkflowAutomations'))
    DROP INDEX IX_WorkflowAutomations_IdAssignee ON WorkflowAutomations;

IF COL_LENGTH('WorkflowAutomations','IdAssignee') IS NOT NULL
    ALTER TABLE WorkflowAutomations DROP COLUMN IdAssignee;
");
        }
    }
}
