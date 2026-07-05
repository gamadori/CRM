using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityAssignee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdAssignee",
                table: "Activities",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Activities_IdAssignee",
                table: "Activities",
                column: "IdAssignee");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_AspNetUsers_IdAssignee",
                table: "Activities",
                column: "IdAssignee",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_AspNetUsers_IdAssignee",
                table: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_Activities_IdAssignee",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "IdAssignee",
                table: "Activities");
        }
    }
}
