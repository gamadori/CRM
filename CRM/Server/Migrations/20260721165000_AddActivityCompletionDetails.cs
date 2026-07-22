using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260721165000_AddActivityCompletionDetails")]
    public partial class AddActivityCompletionDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompletionNotes",
                table: "Activities",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdCompletedBy",
                table: "Activities",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextStep",
                table: "Activities",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Outcome",
                table: "Activities",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Activities_IdCompletedBy",
                table: "Activities",
                column: "IdCompletedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_AspNetUsers_IdCompletedBy",
                table: "Activities",
                column: "IdCompletedBy",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_AspNetUsers_IdCompletedBy",
                table: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_Activities_IdCompletedBy",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "CompletionNotes",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "IdCompletedBy",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "NextStep",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Outcome",
                table: "Activities");
        }
    }
}
