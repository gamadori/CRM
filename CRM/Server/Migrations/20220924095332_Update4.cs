using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    public partial class Update4 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttachmentFiles_Attachments_IdAttachment",
                table: "AttachmentFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_AspNetUsers_IdUser",
                table: "Attachments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Attachments",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "IdUser",
                table: "Projects");

            migrationBuilder.RenameTable(
                name: "Attachments",
                newName: "Attachment");

            migrationBuilder.RenameIndex(
                name: "IX_Attachments_IdUser",
                table: "Attachment",
                newName: "IX_Attachment_IdUser");

            migrationBuilder.AddColumn<string>(
                name: "IdUserCreate",
                table: "Projects",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdUserLeader",
                table: "Projects",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Attachment",
                table: "Attachment",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_IdUserCreate",
                table: "Projects",
                column: "IdUserCreate");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_IdUserLeader",
                table: "Projects",
                column: "IdUserLeader");

            migrationBuilder.AddForeignKey(
                name: "FK_Attachment_AspNetUsers_IdUser",
                table: "Attachment",
                column: "IdUser",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AttachmentFiles_Attachment_IdAttachment",
                table: "AttachmentFiles",
                column: "IdAttachment",
                principalTable: "Attachment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_AspNetUsers_IdUserCreate",
                table: "Projects",
                column: "IdUserCreate",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_AspNetUsers_IdUserLeader",
                table: "Projects",
                column: "IdUserLeader",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attachment_AspNetUsers_IdUser",
                table: "Attachment");

            migrationBuilder.DropForeignKey(
                name: "FK_AttachmentFiles_Attachment_IdAttachment",
                table: "AttachmentFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_AspNetUsers_IdUserCreate",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_AspNetUsers_IdUserLeader",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_IdUserCreate",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_IdUserLeader",
                table: "Projects");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Attachment",
                table: "Attachment");

            migrationBuilder.DropColumn(
                name: "IdUserCreate",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IdUserLeader",
                table: "Projects");

            migrationBuilder.RenameTable(
                name: "Attachment",
                newName: "Attachments");

            migrationBuilder.RenameIndex(
                name: "IX_Attachment_IdUser",
                table: "Attachments",
                newName: "IX_Attachments_IdUser");

            migrationBuilder.AddColumn<int>(
                name: "IdUser",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Attachments",
                table: "Attachments",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AttachmentFiles_Attachments_IdAttachment",
                table: "AttachmentFiles",
                column: "IdAttachment",
                principalTable: "Attachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_AspNetUsers_IdUser",
                table: "Attachments",
                column: "IdUser",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
