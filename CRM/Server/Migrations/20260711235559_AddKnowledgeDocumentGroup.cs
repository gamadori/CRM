using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeDocumentGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DocumentGroupId",
                table: "ProductKnowledge",
                type: "uniqueidentifier",
                nullable: true);

            // Valorizzazione retroattiva: le voci già importate vengono raggruppate per
            // (documento sorgente, modello, istante di creazione) — chiave che le parti di uno
            // stesso import condividono — e a ciascun gruppo viene assegnato un GUID condiviso.
            // Le voci manuali (SourceDocument NULL) restano senza gruppo.
            migrationBuilder.Sql(@"
;WITH grp AS (
    SELECT SourceDocument, IdProduct, CreatedAt, NEWID() AS gid
    FROM ProductKnowledge
    WHERE SourceDocument IS NOT NULL
    GROUP BY SourceDocument, IdProduct, CreatedAt
)
UPDATE pk
SET pk.DocumentGroupId = grp.gid
FROM ProductKnowledge pk
INNER JOIN grp
    ON pk.SourceDocument = grp.SourceDocument
   AND ISNULL(pk.IdProduct, -1) = ISNULL(grp.IdProduct, -1)
   AND pk.CreatedAt = grp.CreatedAt
WHERE pk.SourceDocument IS NOT NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentGroupId",
                table: "ProductKnowledge");
        }
    }
}
