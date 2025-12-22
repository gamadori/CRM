using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    public partial class Update19 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "IdUserOpened",
                table: "Tickets",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IdUserClosed",
                table: "Tickets",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_IdUserClosed",
                table: "Tickets",
                column: "IdUserClosed");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_IdUserOpened",
                table: "Tickets",
                column: "IdUserOpened");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_AspNetUsers_IdUserClosed",
                table: "Tickets",
                column: "IdUserClosed",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_AspNetUsers_IdUserOpened",
                table: "Tickets",
                column: "IdUserOpened",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_AspNetUsers_IdUserClosed",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_AspNetUsers_IdUserOpened",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_IdUserClosed",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_IdUserOpened",
                table: "Tickets");

            migrationBuilder.AlterColumn<string>(
                name: "IdUserOpened",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IdUserClosed",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
