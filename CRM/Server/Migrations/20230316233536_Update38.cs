using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class Update38 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Free",
                table: "TicketTypes");

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "TicketTypes",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Price",
                table: "TicketTypes");

            migrationBuilder.AddColumn<bool>(
                name: "Free",
                table: "TicketTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
