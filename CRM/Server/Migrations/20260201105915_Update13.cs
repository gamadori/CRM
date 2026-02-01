using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class Update13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBillable",
                table: "TicketInterventionTimes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "TicketInterventionTimes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeType",
                table: "TicketInterventionTimes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TravelKilometers",
                table: "TicketInterventionTimes",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBillable",
                table: "TicketInterventionTimes");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "TicketInterventionTimes");

            migrationBuilder.DropColumn(
                name: "TimeType",
                table: "TicketInterventionTimes");

            migrationBuilder.DropColumn(
                name: "TravelKilometers",
                table: "TicketInterventionTimes");
        }
    }
}
