using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class Update64 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdParameter",
                table: "BackUpParameters",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BackUpParameters_IdParameter",
                table: "BackUpParameters",
                column: "IdParameter");

            migrationBuilder.AddForeignKey(
                name: "FK_BackUpParameters_ProductParameters_IdParameter",
                table: "BackUpParameters",
                column: "IdParameter",
                principalTable: "ProductParameters",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BackUpParameters_ProductParameters_IdParameter",
                table: "BackUpParameters");

            migrationBuilder.DropIndex(
                name: "IX_BackUpParameters_IdParameter",
                table: "BackUpParameters");

            migrationBuilder.DropColumn(
                name: "IdParameter",
                table: "BackUpParameters");
        }
    }
}
