using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class Update16 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketsInterventions_AspNetUsers_IdUser",
                table: "TicketsInterventions");

            migrationBuilder.DropIndex(
                name: "IX_TicketsInterventions_IdUser",
                table: "TicketsInterventions");

            migrationBuilder.DropColumn(
                name: "IdUser",
                table: "TicketsInterventions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdUser",
                table: "TicketsInterventions",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_TicketsInterventions_IdUser",
                table: "TicketsInterventions",
                column: "IdUser");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketsInterventions_AspNetUsers_IdUser",
                table: "TicketsInterventions",
                column: "IdUser",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
