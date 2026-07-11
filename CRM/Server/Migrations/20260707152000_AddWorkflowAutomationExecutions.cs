using System;
using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260707152000_AddWorkflowAutomationExecutions")]
    public partial class AddWorkflowAutomationExecutions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowAutomationExecutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdWorkflowAutomation = table.Column<int>(type: "int", nullable: false),
                    Trigger = table.Column<int>(type: "int", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdActivity = table.Column<int>(type: "int", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowAutomationExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowAutomationExecutions_Activities_IdActivity",
                        column: x => x.IdActivity,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WorkflowAutomationExecutions_WorkflowAutomations_IdWorkflowAutomation",
                        column: x => x.IdWorkflowAutomation,
                        principalTable: "WorkflowAutomations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowAutomationExecutions_ExecutedAt",
                table: "WorkflowAutomationExecutions",
                column: "ExecutedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowAutomationExecutions_IdActivity",
                table: "WorkflowAutomationExecutions",
                column: "IdActivity");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowAutomationExecutions_IdWorkflowAutomation_Trigger_EntityType_EntityId",
                table: "WorkflowAutomationExecutions",
                columns: new[] { "IdWorkflowAutomation", "Trigger", "EntityType", "EntityId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowAutomationExecutions");
        }
    }
}
