using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// La firma dell'intervento smette di dipendere da un ramo implicito nel codice e diventa una
    /// configurazione per tipo di intervento, congelata poi sul singolo intervento.
    /// <para>
    /// I valori iniziali riproducono cio' che la vecchia impostazione prometteva a parole
    /// ("interventi Telefono/Web/Remoto"), non cio' che faceva: Sul Posto e Ufficio firmano sul
    /// dispositivo, Telefono/Web/Remoto da remoto, mentre il Workshop resta senza firma. Prima
    /// finiva anche lui nella firma remota, perche' il codice trattava come remoto tutto cio' che
    /// non era Sul Posto o Ufficio.
    /// </para>
    /// <para>
    /// L'ultimo passo e' una correzione di dati: gli interventi che risultano "in attesa di firma"
    /// solo perche' il valore di partenza dell'enum era Pending. Si toccano solo quelli senza
    /// firma, senza link aperto e senza OTP in corso, cioe' quelli in cui a nessuno e' mai stato
    /// chiesto niente.
    /// </para>
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260809120000_AddSignatureRequirement")]
    public partial class AddSignatureRequirement : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupportTypeSettings",
                columns: table => new
                {
                    SupportType = table.Column<int>(type: "int", nullable: false),
                    SignatureRequirement = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTypeSettings", x => x.SupportType);
                });

            migrationBuilder.AddColumn<int>(
                name: "SignatureRequirement",
                table: "TicketsInterventions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // 0=None 1=OnDevice 2=Remote. Phone=0, Web=1, OnSite=2, Office=3, Remote=4, Workshop=5.
            migrationBuilder.Sql(@"
IF NOT EXISTS(SELECT 1 FROM SupportTypeSettings)
    INSERT INTO SupportTypeSettings (SupportType, SignatureRequirement) VALUES
        (0, 2),
        (1, 2),
        (2, 1),
        (3, 1),
        (4, 2),
        (5, 0);
");

            // Batch a se': la colonna appena aggiunta non e' visibile nello stesso lotto.
            migrationBuilder.Sql(@"
UPDATE ti
SET ti.SignatureRequirement = ISNULL(sts.SignatureRequirement, 0)
FROM TicketsInterventions ti
LEFT JOIN SupportTypeSettings sts ON sts.SupportType = ti.SupportType;
");

            // Chi non ha mai avuto una firma chiesta non e' "in attesa": e' senza firma prevista.
            // Gli interventi con una firma acquisita, un link aperto o un OTP in corso restano
            // com'erano.
            migrationBuilder.Sql(@"
UPDATE TicketsInterventions
SET SignatureStatus = 3
WHERE SignatureStatus = 0
  AND CustomerSignature IS NULL
  AND PendingSignature IS NULL
  AND SignatureConfirmationToken IS NULL
  AND SignatureOtpChallengeId IS NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Si torna al significato di prima: "non richiesta" non esisteva, era Pending.
            migrationBuilder.Sql("UPDATE TicketsInterventions SET SignatureStatus = 0 WHERE SignatureStatus = 3;");

            migrationBuilder.DropColumn(
                name: "SignatureRequirement",
                table: "TicketsInterventions");

            migrationBuilder.DropTable(name: "SupportTypeSettings");
        }
    }
}
