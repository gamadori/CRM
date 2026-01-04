using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class update2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArticleEvents_ArticleDomains_DomainId",
                table: "ArticleEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_ArticleEvents_ArticleEventTypes_EventTypeId",
                table: "ArticleEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_ArticleStates_ArticleDomains_DomainId",
                table: "ArticleStates");

            migrationBuilder.AddForeignKey(
                name: "FK_ArticleEvents_ArticleDomains_DomainId",
                table: "ArticleEvents",
                column: "DomainId",
                principalTable: "ArticleDomains",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ArticleEvents_ArticleEventTypes_EventTypeId",
                table: "ArticleEvents",
                column: "EventTypeId",
                principalTable: "ArticleEventTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ArticleStates_ArticleDomains_DomainId",
                table: "ArticleStates",
                column: "DomainId",
                principalTable: "ArticleDomains",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArticleEvents_ArticleDomains_DomainId",
                table: "ArticleEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_ArticleEvents_ArticleEventTypes_EventTypeId",
                table: "ArticleEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_ArticleStates_ArticleDomains_DomainId",
                table: "ArticleStates");

            migrationBuilder.AddForeignKey(
                name: "FK_ArticleEvents_ArticleDomains_DomainId",
                table: "ArticleEvents",
                column: "DomainId",
                principalTable: "ArticleDomains",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArticleEvents_ArticleEventTypes_EventTypeId",
                table: "ArticleEvents",
                column: "EventTypeId",
                principalTable: "ArticleEventTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArticleStates_ArticleDomains_DomainId",
                table: "ArticleStates",
                column: "DomainId",
                principalTable: "ArticleDomains",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
