using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260727090000_AddPasswordResetEmailTemplate")]
    public partial class AddPasswordResetEmailTemplate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM EmailTemplates WHERE Tipo = 15 AND ISNULL(Language, '') = 'it')
BEGIN
    INSERT INTO EmailTemplates (Tipo, Language, Subject, Body, IdLogo)
    VALUES (
        15,
        'it',
        N'Reset password',
        N'<p>Ciao $NAME,</p>
<p>abbiamo ricevuto una richiesta di reset della password per il tuo account.</p>
<p>Per scegliere una nuova password clicca sul link qui sotto:</p>
<p><a href=""$URL"">Reimposta password</a></p>
<p>Se non hai richiesto tu questa operazione, puoi ignorare questa email.</p>',
        NULL
    );
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM EmailTemplates
WHERE Tipo = 15
  AND ISNULL(Language, '') = 'it'
  AND Subject = N'Reset password';
");
        }
    }
}
