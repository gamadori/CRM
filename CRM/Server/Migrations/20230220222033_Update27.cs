using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    public partial class Update27 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Accessories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ProductTypeAccessories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdProduct = table.Column<int>(type: "int", nullable: false),
                    IdAccessoryType = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Necessary = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductTypeAccessories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductTypeAccessories_AccessoryTypes_IdAccessoryType",
                        column: x => x.IdAccessoryType,
                        principalTable: "AccessoryTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductTypeAccessories_Products_IdProduct",
                        column: x => x.IdProduct,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductTypeAccessoryLanguages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdProductTypeAccessory = table.Column<int>(type: "int", nullable: false),
                    IdLanguage = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductTypeAccessoryLanguages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductTypeAccessoryLanguages_Languages_IdLanguage",
                        column: x => x.IdLanguage,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductTypeAccessoryLanguages_ProductTypeAccessories_IdProductTypeAccessory",
                        column: x => x.IdProductTypeAccessory,
                        principalTable: "ProductTypeAccessories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductTypeAccessories_IdAccessoryType",
                table: "ProductTypeAccessories",
                column: "IdAccessoryType");

            migrationBuilder.CreateIndex(
                name: "IX_ProductTypeAccessories_IdProduct",
                table: "ProductTypeAccessories",
                column: "IdProduct");

            migrationBuilder.CreateIndex(
                name: "IX_ProductTypeAccessoryLanguages_IdLanguage",
                table: "ProductTypeAccessoryLanguages",
                column: "IdLanguage");

            migrationBuilder.CreateIndex(
                name: "IX_ProductTypeAccessoryLanguages_IdProductTypeAccessory",
                table: "ProductTypeAccessoryLanguages",
                column: "IdProductTypeAccessory");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductTypeAccessoryLanguages");

            migrationBuilder.DropTable(
                name: "ProductTypeAccessories");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Accessories",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
