using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailActivityLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdEmailSent",
                table: "Activities",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Activities_IdEmailSent",
                table: "Activities",
                column: "IdEmailSent");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_EmailsSent_IdEmailSent",
                table: "Activities",
                column: "IdEmailSent",
                principalTable: "EmailsSent",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_EmailsSent_IdEmailSent",
                table: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_Activities_IdEmailSent",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "IdEmailSent",
                table: "Activities");
        }
    }
}
