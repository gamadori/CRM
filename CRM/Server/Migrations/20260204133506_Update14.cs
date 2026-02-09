using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class Update14 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rimuovi la foreign key constraint
            migrationBuilder.DropForeignKey(
                name: "FK_TicketsInterventions_AspNetUsers_IdUser",
                table: "TicketsInterventions");

            // Rimuovi l'indice
            migrationBuilder.DropIndex(
                name: "IX_TicketsInterventions_IdUser",
                table: "TicketsInterventions");

            // Rimuovi la colonna IdUser
            migrationBuilder.DropColumn(
                name: "IdUser",
                table: "TicketsInterventions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ripristina la colonna
            migrationBuilder.AddColumn<string>(
                name: "IdUser",
                table: "TicketsInterventions",
                type: "nvarchar(450)",
                nullable: true);

            // Ripristina l'indice
            migrationBuilder.CreateIndex(
                name: "IX_TicketsInterventions_IdUser",
                table: "TicketsInterventions",
                column: "IdUser");

            // Ripristina la foreign key
            migrationBuilder.AddForeignKey(
                name: "FK_TicketsInterventions_AspNetUsers_IdUser",
                table: "TicketsInterventions",
                column: "IdUser",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
