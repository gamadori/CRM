using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailTemplateLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "EmailTemplates",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_Tipo_Language",
                table: "EmailTemplates",
                columns: new[] { "Tipo", "Language" },
                unique: true,
                filter: "[Language] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailTemplates_Tipo_Language",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "EmailTemplates");
        }
    }
}
