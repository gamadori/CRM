using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleRecipient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdRecipientCompany",
                table: "Articles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientCountry",
                table: "Articles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Articles_IdRecipientCompany",
                table: "Articles",
                column: "IdRecipientCompany");

            migrationBuilder.AddForeignKey(
                name: "FK_Articles_Companies_IdRecipientCompany",
                table: "Articles",
                column: "IdRecipientCompany",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Articles_Companies_IdRecipientCompany",
                table: "Articles");

            migrationBuilder.DropIndex(
                name: "IX_Articles_IdRecipientCompany",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "IdRecipientCompany",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "RecipientCountry",
                table: "Articles");
        }
    }
}
