using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddLicenseFeatureProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ArticleLicenseFeatureDefs_Key_IdProductType",
                table: "ArticleLicenseFeatureDefs");

            migrationBuilder.AddColumn<int>(
                name: "IdProduct",
                table: "ArticleLicenseFeatureDefs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArticleLicenseFeatureDefs_IdProduct",
                table: "ArticleLicenseFeatureDefs",
                column: "IdProduct");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleLicenseFeatureDefs_Key_IdProductType_IdProduct",
                table: "ArticleLicenseFeatureDefs",
                columns: new[] { "Key", "IdProductType", "IdProduct" },
                unique: true,
                filter: "[IdProductType] IS NOT NULL AND [IdProduct] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ArticleLicenseFeatureDefs_Products_IdProduct",
                table: "ArticleLicenseFeatureDefs",
                column: "IdProduct",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArticleLicenseFeatureDefs_Products_IdProduct",
                table: "ArticleLicenseFeatureDefs");

            migrationBuilder.DropIndex(
                name: "IX_ArticleLicenseFeatureDefs_IdProduct",
                table: "ArticleLicenseFeatureDefs");

            migrationBuilder.DropIndex(
                name: "IX_ArticleLicenseFeatureDefs_Key_IdProductType_IdProduct",
                table: "ArticleLicenseFeatureDefs");

            migrationBuilder.DropColumn(
                name: "IdProduct",
                table: "ArticleLicenseFeatureDefs");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleLicenseFeatureDefs_Key_IdProductType",
                table: "ArticleLicenseFeatureDefs",
                columns: new[] { "Key", "IdProductType" },
                unique: true,
                filter: "[IdProductType] IS NOT NULL");
        }
    }
}
