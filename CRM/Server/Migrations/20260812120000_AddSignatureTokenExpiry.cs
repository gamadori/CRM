using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <summary>
    /// Il link di firma mandato al cliente ha una scadenza.
    /// <para>
    /// Prima il token valeva finche' non veniva usato, cioe' per sempre: chi ritrovava quella email
    /// mesi dopo poteva ancora firmare un verbale di marzo. La finestra e' configurabile in
    /// GlobalSettings (<c>SignatureLinkValidityDays</c>, 7 giorni salvo diversa indicazione).
    /// </para>
    /// <para>
    /// Le righe che hanno gia' un token in circolazione ricevono una <b>finestra di grazia</b> di
    /// 7 giorni da adesso invece di nascere scadute: sono link mandati a clienti veri, e farli
    /// morire tutti insieme al deploy avrebbe significato un giro di telefonate. Le altre restano
    /// a NULL, che il codice legge come "scaduto" - vuota non vale "senza scadenza", altrimenti un
    /// percorso che dimenticasse di impostarla tornerebbe in silenzio al link eterno.
    /// </para>
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260812120000_AddSignatureTokenExpiry")]
    public partial class AddSignatureTokenExpiry : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'SignatureTokenExpiry' AND object_id = OBJECT_ID('dbo.TicketsInterventions'))
    ALTER TABLE [dbo].[TicketsInterventions] ADD [SignatureTokenExpiry] datetime2 NULL;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'SignatureLinkValidityDays' AND object_id = OBJECT_ID('dbo.GlobalSettings'))
    ALTER TABLE [dbo].[GlobalSettings] ADD [SignatureLinkValidityDays] int NOT NULL CONSTRAINT [DF_GlobalSettings_SignatureLinkValidityDays] DEFAULT 7;
");

            // Batch a se': le colonne appena aggiunte non sono visibili nello stesso lotto.
            migrationBuilder.Sql(@"
UPDATE [dbo].[TicketsInterventions]
   SET [SignatureTokenExpiry] = DATEADD(day, 7, GETDATE())
 WHERE [SignatureConfirmationToken] IS NOT NULL
   AND [SignatureTokenExpiry] IS NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_GlobalSettings_SignatureLinkValidityDays')
    ALTER TABLE [dbo].[GlobalSettings] DROP CONSTRAINT [DF_GlobalSettings_SignatureLinkValidityDays];
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'SignatureLinkValidityDays' AND object_id = OBJECT_ID('dbo.GlobalSettings'))
    ALTER TABLE [dbo].[GlobalSettings] DROP COLUMN [SignatureLinkValidityDays];
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'SignatureTokenExpiry' AND object_id = OBJECT_ID('dbo.TicketsInterventions'))
    ALTER TABLE [dbo].[TicketsInterventions] DROP COLUMN [SignatureTokenExpiry];
");
        }
    }
}
