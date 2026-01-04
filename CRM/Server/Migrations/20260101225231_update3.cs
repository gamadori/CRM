using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class update3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArticleDomainStates_Articles_ArticleId",
                table: "ArticleDomainStates");

            migrationBuilder.AlterColumn<int>(
                name: "ArticleId",
                table: "ArticleDomainStates",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ArticleDomainStates_Articles_ArticleId",
                table: "ArticleDomainStates",
                column: "ArticleId",
                principalTable: "Articles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArticleDomainStates_Articles_ArticleId",
                table: "ArticleDomainStates");

            migrationBuilder.AlterColumn<int>(
                name: "ArticleId",
                table: "ArticleDomainStates",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_ArticleDomainStates_Articles_ArticleId",
                table: "ArticleDomainStates",
                column: "ArticleId",
                principalTable: "Articles",
                principalColumn: "Id");
        }
    }
}
