using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Consolida il modello dell'azienda madre su un'unica fonte di verità:
    /// <c>Company.CompanyType = HeadCompany</c>. Elimina i due doppioni storici
    /// <c>GlobalSetting.IdHeadQuarter</c> (puntatore ridondante) e <c>Company.Master</c>
    /// (flag mai usato in logica).
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260720150000_CleanupHeadCompanyModel")]
    public partial class CleanupHeadCompanyModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Riconciliazione: se nessuna azienda è ancora tipizzata HeadCompany, promuove
            // quella finora indicata da IdHeadQuarter. Se una HeadCompany esiste già, non
            // tocca nulla (evita di crearne due). CompanyTypes.HeadCompany = 2.
            migrationBuilder.Sql(@"
                UPDATE [Companies]
                SET [CompanyType] = 2
                WHERE [Id] IN (SELECT [IdHeadQuarter] FROM [GlobalSettings])
                  AND NOT EXISTS (SELECT 1 FROM [Companies] WHERE [CompanyType] = 2);");

            migrationBuilder.DropColumn(name: "IdHeadQuarter", table: "GlobalSettings");
            migrationBuilder.DropColumn(name: "Master", table: "Companies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Master",
                table: "Companies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "IdHeadQuarter",
                table: "GlobalSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Ripristino best-effort del puntatore a partire dall'azienda madre tipizzata.
            migrationBuilder.Sql(@"
                UPDATE [GlobalSettings]
                SET [IdHeadQuarter] = ISNULL((SELECT MIN([Id]) FROM [Companies] WHERE [CompanyType] = 2), 0);");
        }
    }
}
