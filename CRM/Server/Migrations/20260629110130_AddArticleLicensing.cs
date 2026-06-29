using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleLicensing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArticleLicenseFeatureDefs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ValueType = table.Column<int>(type: "int", nullable: false),
                    DefaultValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IdProductType = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleLicenseFeatureDefs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleLicenseFeatureDefs_ProductTypes_IdProductType",
                        column: x => x.IdProductType,
                        principalTable: "ProductTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArticleLicenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdArticle = table.Column<int>(type: "int", nullable: false),
                    MachineKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleLicenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleLicenses_Articles_IdArticle",
                        column: x => x.IdArticle,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArticleLicenseFeatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdLicense = table.Column<int>(type: "int", nullable: false),
                    IdFeatureDef = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleLicenseFeatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleLicenseFeatures_ArticleLicenseFeatureDefs_IdFeatureDef",
                        column: x => x.IdFeatureDef,
                        principalTable: "ArticleLicenseFeatureDefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArticleLicenseFeatures_ArticleLicenses_IdLicense",
                        column: x => x.IdLicense,
                        principalTable: "ArticleLicenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleLicenseFeatureDefs_IdProductType",
                table: "ArticleLicenseFeatureDefs",
                column: "IdProductType");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleLicenseFeatureDefs_Key_IdProductType",
                table: "ArticleLicenseFeatureDefs",
                columns: new[] { "Key", "IdProductType" },
                unique: true,
                filter: "[IdProductType] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleLicenseFeatures_IdFeatureDef",
                table: "ArticleLicenseFeatures",
                column: "IdFeatureDef");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleLicenseFeatures_IdLicense_IdFeatureDef",
                table: "ArticleLicenseFeatures",
                columns: new[] { "IdLicense", "IdFeatureDef" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArticleLicenses_IdArticle",
                table: "ArticleLicenses",
                column: "IdArticle",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArticleLicenses_MachineKey",
                table: "ArticleLicenses",
                column: "MachineKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleLicenseFeatures");

            migrationBuilder.DropTable(
                name: "ArticleLicenseFeatureDefs");

            migrationBuilder.DropTable(
                name: "ArticleLicenses");
        }
    }
}
