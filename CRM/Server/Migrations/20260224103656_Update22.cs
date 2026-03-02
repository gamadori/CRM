using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class Update22 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FolderId",
                table: "Attachment",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "Attachment",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Attachment_FolderId",
                table: "Attachment",
                column: "FolderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Attachment_Folders_FolderId",
                table: "Attachment",
                column: "FolderId",
                principalTable: "Folders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attachment_Folders_FolderId",
                table: "Attachment");

            migrationBuilder.DropIndex(
                name: "IX_Attachment_FolderId",
                table: "Attachment");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "Attachment");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "Attachment");
        }
    }
}
