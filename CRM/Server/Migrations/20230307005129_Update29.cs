using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    public partial class Update29 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductTypeAccessories_AccessoryTypes_IdAccessoryType",
                table: "ProductTypeAccessories");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductTypeAccessories_Products_IdProduct",
                table: "ProductTypeAccessories");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductTypeAccessoryLanguages_ProductTypeAccessories_IdProductTypeAccessory",
                table: "ProductTypeAccessoryLanguages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductTypeAccessories",
                table: "ProductTypeAccessories");

            migrationBuilder.RenameTable(
                name: "ProductTypeAccessories",
                newName: "ProductAccessoryTypes");

            migrationBuilder.RenameColumn(
                name: "IdProductTypeAccessory",
                table: "ProductTypeAccessoryLanguages",
                newName: "IdProdAccType");

            migrationBuilder.RenameIndex(
                name: "IX_ProductTypeAccessoryLanguages_IdProductTypeAccessory",
                table: "ProductTypeAccessoryLanguages",
                newName: "IX_ProductTypeAccessoryLanguages_IdProdAccType");

            migrationBuilder.RenameIndex(
                name: "IX_ProductTypeAccessories_IdProduct",
                table: "ProductAccessoryTypes",
                newName: "IX_ProductAccessoryTypes_IdProduct");

            migrationBuilder.RenameIndex(
                name: "IX_ProductTypeAccessories_IdAccessoryType",
                table: "ProductAccessoryTypes",
                newName: "IX_ProductAccessoryTypes_IdAccessoryType");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductAccessoryTypes",
                table: "ProductAccessoryTypes",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "ArticleAccessory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdArticle = table.Column<int>(type: "int", nullable: false),
                    IdAccessory = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleAccessory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleAccessory_Accessories_IdAccessory",
                        column: x => x.IdAccessory,
                        principalTable: "Accessories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArticleAccessory_Articles_IdArticle",
                        column: x => x.IdArticle,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleAccessory_IdAccessory",
                table: "ArticleAccessory",
                column: "IdAccessory");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleAccessory_IdArticle",
                table: "ArticleAccessory",
                column: "IdArticle");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductAccessoryTypes_AccessoryTypes_IdAccessoryType",
                table: "ProductAccessoryTypes",
                column: "IdAccessoryType",
                principalTable: "AccessoryTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductAccessoryTypes_Products_IdProduct",
                table: "ProductAccessoryTypes",
                column: "IdProduct",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductTypeAccessoryLanguages_ProductAccessoryTypes_IdProdAccType",
                table: "ProductTypeAccessoryLanguages",
                column: "IdProdAccType",
                principalTable: "ProductAccessoryTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductAccessoryTypes_AccessoryTypes_IdAccessoryType",
                table: "ProductAccessoryTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductAccessoryTypes_Products_IdProduct",
                table: "ProductAccessoryTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductTypeAccessoryLanguages_ProductAccessoryTypes_IdProdAccType",
                table: "ProductTypeAccessoryLanguages");

            migrationBuilder.DropTable(
                name: "ArticleAccessory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductAccessoryTypes",
                table: "ProductAccessoryTypes");

            migrationBuilder.RenameTable(
                name: "ProductAccessoryTypes",
                newName: "ProductTypeAccessories");

            migrationBuilder.RenameColumn(
                name: "IdProdAccType",
                table: "ProductTypeAccessoryLanguages",
                newName: "IdProductTypeAccessory");

            migrationBuilder.RenameIndex(
                name: "IX_ProductTypeAccessoryLanguages_IdProdAccType",
                table: "ProductTypeAccessoryLanguages",
                newName: "IX_ProductTypeAccessoryLanguages_IdProductTypeAccessory");

            migrationBuilder.RenameIndex(
                name: "IX_ProductAccessoryTypes_IdProduct",
                table: "ProductTypeAccessories",
                newName: "IX_ProductTypeAccessories_IdProduct");

            migrationBuilder.RenameIndex(
                name: "IX_ProductAccessoryTypes_IdAccessoryType",
                table: "ProductTypeAccessories",
                newName: "IX_ProductTypeAccessories_IdAccessoryType");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductTypeAccessories",
                table: "ProductTypeAccessories",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductTypeAccessories_AccessoryTypes_IdAccessoryType",
                table: "ProductTypeAccessories",
                column: "IdAccessoryType",
                principalTable: "AccessoryTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductTypeAccessories_Products_IdProduct",
                table: "ProductTypeAccessories",
                column: "IdProduct",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductTypeAccessoryLanguages_ProductTypeAccessories_IdProductTypeAccessory",
                table: "ProductTypeAccessoryLanguages",
                column: "IdProductTypeAccessory",
                principalTable: "ProductTypeAccessories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
