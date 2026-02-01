using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class Update12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InterventionTypeLanguages_InterventionTypes_InterventionTypeId",
                table: "InterventionTypeLanguages");

            migrationBuilder.DropForeignKey(
                name: "FK_InterventionTypeTicketIntervention_TicketsInterventions_TicketsInterventionTypeId",
                table: "InterventionTypeTicketIntervention");

            migrationBuilder.DropIndex(
                name: "IX_InterventionTypeLanguages_InterventionTypeId",
                table: "InterventionTypeLanguages");

            migrationBuilder.DropColumn(
                name: "InterventionTypeId",
                table: "InterventionTypeLanguages");

            migrationBuilder.RenameColumn(
                name: "TicketsInterventionTypeId",
                table: "InterventionTypeTicketIntervention",
                newName: "TicketsInterventionsId");

            migrationBuilder.RenameIndex(
                name: "IX_InterventionTypeTicketIntervention_TicketsInterventionTypeId",
                table: "InterventionTypeTicketIntervention",
                newName: "IX_InterventionTypeTicketIntervention_TicketsInterventionsId");

            migrationBuilder.CreateIndex(
                name: "IX_InterventionTypeLanguages_IdInterventionType",
                table: "InterventionTypeLanguages",
                column: "IdInterventionType");

            migrationBuilder.AddForeignKey(
                name: "FK_InterventionTypeLanguages_InterventionTypes_IdInterventionType",
                table: "InterventionTypeLanguages",
                column: "IdInterventionType",
                principalTable: "InterventionTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InterventionTypeTicketIntervention_TicketsInterventions_TicketsInterventionsId",
                table: "InterventionTypeTicketIntervention",
                column: "TicketsInterventionsId",
                principalTable: "TicketsInterventions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InterventionTypeLanguages_InterventionTypes_IdInterventionType",
                table: "InterventionTypeLanguages");

            migrationBuilder.DropForeignKey(
                name: "FK_InterventionTypeTicketIntervention_TicketsInterventions_TicketsInterventionsId",
                table: "InterventionTypeTicketIntervention");

            migrationBuilder.DropIndex(
                name: "IX_InterventionTypeLanguages_IdInterventionType",
                table: "InterventionTypeLanguages");

            migrationBuilder.RenameColumn(
                name: "TicketsInterventionsId",
                table: "InterventionTypeTicketIntervention",
                newName: "TicketsInterventionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_InterventionTypeTicketIntervention_TicketsInterventionsId",
                table: "InterventionTypeTicketIntervention",
                newName: "IX_InterventionTypeTicketIntervention_TicketsInterventionTypeId");

            migrationBuilder.AddColumn<int>(
                name: "InterventionTypeId",
                table: "InterventionTypeLanguages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterventionTypeLanguages_InterventionTypeId",
                table: "InterventionTypeLanguages",
                column: "InterventionTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_InterventionTypeLanguages_InterventionTypes_InterventionTypeId",
                table: "InterventionTypeLanguages",
                column: "InterventionTypeId",
                principalTable: "InterventionTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InterventionTypeTicketIntervention_TicketsInterventions_TicketsInterventionTypeId",
                table: "InterventionTypeTicketIntervention",
                column: "TicketsInterventionTypeId",
                principalTable: "TicketsInterventions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
