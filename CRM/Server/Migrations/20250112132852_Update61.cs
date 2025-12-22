using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class Update61 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BackUpParameters_ProductParameter_IdParameter",
                table: "BackUpParameters");

            migrationBuilder.DropTable(
                name: "ProductParameter");

            migrationBuilder.DropIndex(
                name: "IX_BackUpParameters_IdParameter",
                table: "BackUpParameters");

            migrationBuilder.DropColumn(
                name: "IdParameter",
                table: "BackUpParameters");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "BackUpParameters",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "BackUpParameters");

            migrationBuilder.AddColumn<int>(
                name: "IdParameter",
                table: "BackUpParameters",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductParameter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdProduct = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Max = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Min = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueDefault = table.Column<string>(type: "nvarchar(max)", nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "IX_BackUpParameters_IdParameter",
                table: "BackUpParameters",
                column: "IdParameter");

            migrationBuilder.CreateIndex(
                name: "IX_ProductParameter_IdProduct",
                table: "ProductParameter",
                column: "IdProduct");

            migrationBuilder.AddForeignKey(
                name: "FK_BackUpParameters_ProductParameter_IdParameter",
                table: "BackUpParameters",
                column: "IdParameter",
                principalTable: "ProductParameter",
                principalColumn: "Id");
        }
    }
}
