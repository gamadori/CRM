using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260726173000_AddTicketBlockEmailTemplates")]
    public partial class AddTicketBlockEmailTemplates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM EmailTemplates WHERE Tipo = 13 AND ISNULL(Language, '') = 'it')
BEGIN
    INSERT INTO EmailTemplates (Tipo, Language, Subject, Body, IdLogo)
    VALUES (
        13,
        'it',
        N'Ticket $TICKET bloccato',
        N'<p>Ciao $NAME,</p>
<p>il ticket <strong>$TICKET</strong> e'' stato segnalato come <strong>bloccato</strong>.</p>
<p><strong>Cliente:</strong> $COMPANY<br />
<strong>Commessa:</strong> $COMMESSA<br />
<strong>Fase:</strong> $PHASE<br />
<strong>Motivo:</strong> $REASON</p>
<p><a href=""$URL"">Apri il ticket</a></p>',
        NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM EmailTemplates WHERE Tipo = 14 AND ISNULL(Language, '') = 'it')
BEGIN
    INSERT INTO EmailTemplates (Tipo, Language, Subject, Body, IdLogo)
    VALUES (
        14,
        'it',
        N'Ticket $TICKET sbloccato',
        N'<p>Ciao $NAME,</p>
<p>il blocco sul ticket <strong>$TICKET</strong> e'' stato risolto.</p>
<p><strong>Cliente:</strong> $COMPANY<br />
<strong>Commessa:</strong> $COMMESSA<br />
<strong>Fase:</strong> $PHASE<br />
<strong>Nota:</strong> $REASON</p>
<p><a href=""$URL"">Apri il ticket</a></p>',
        NULL
    );
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM EmailTemplates
WHERE Tipo IN (13, 14)
  AND ISNULL(Language, '') = 'it'
  AND Subject IN (N'Ticket $TICKET bloccato', N'Ticket $TICKET sbloccato');
");
        }
    }
}
