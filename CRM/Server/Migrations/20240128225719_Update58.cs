using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class Update58 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArticleBackups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdArticle = table.Column<int>(type: "int", nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleBackups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleBackups_Articles_IdArticle",
                        column: x => x.IdArticle,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductParameter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdProduct = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueDefault = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Min = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Max = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductParameter", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductParameter_Products_IdProduct",
                        column: x => x.IdProduct,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BackUpParameters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdBackUp = table.Column<int>(type: "int", nullable: false),
                    IdParameter = table.Column<int>(type: "int", nullable: true),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackUpParameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackUpParameters_ArticleBackups_IdBackUp",
                        column: x => x.IdBackUp,
                        principalTable: "ArticleBackups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BackUpParameters_ProductParameter_IdParameter",
                        column: x => x.IdParameter,
                        principalTable: "ProductParameter",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleBackups_IdArticle",
                table: "ArticleBackups",
                column: "IdArticle");

            migrationBuilder.CreateIndex(
                name: "IX_BackUpParameters_IdBackUp",
                table: "BackUpParameters",
                column: "IdBackUp");

            migrationBuilder.CreateIndex(
                name: "IX_BackUpParameters_IdParameter",
                table: "BackUpParameters",
                column: "IdParameter");

            migrationBuilder.CreateIndex(
                name: "IX_ProductParameter_IdProduct",
                table: "ProductParameter",
                column: "IdProduct");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackUpParameters");

            migrationBuilder.DropTable(
                name: "ArticleBackups");

            migrationBuilder.DropTable(
                name: "ProductParameter");
        }
    }
}
