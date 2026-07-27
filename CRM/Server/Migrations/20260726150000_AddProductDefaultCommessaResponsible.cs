using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260726150000_AddProductDefaultCommessaResponsible")]
    public partial class AddProductDefaultCommessaResponsible : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Products','IdDefaultCommessaResponsible') IS NULL
    ALTER TABLE Products ADD IdDefaultCommessaResponsible nvarchar(450) NULL;

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Products_IdDefaultCommessaResponsible' AND object_id=OBJECT_ID('Products'))
    CREATE INDEX IX_Products_IdDefaultCommessaResponsible ON Products(IdDefaultCommessaResponsible);

IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_Products_AspNetUsers_IdDefaultCommessaResponsible')
    ALTER TABLE Products ADD CONSTRAINT FK_Products_AspNetUsers_IdDefaultCommessaResponsible
        FOREIGN KEY (IdDefaultCommessaResponsible) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @fk sysname;
SELECT @fk = fk.name FROM sys.foreign_keys fk
 WHERE fk.parent_object_id = OBJECT_ID('Products')
   AND EXISTS (SELECT 1 FROM sys.foreign_key_columns fkc
               JOIN sys.columns c ON c.object_id=fkc.parent_object_id AND c.column_id=fkc.parent_column_id
               WHERE fkc.constraint_object_id=fk.object_id AND c.name='IdDefaultCommessaResponsible');
IF @fk IS NOT NULL EXEC('ALTER TABLE Products DROP CONSTRAINT ' + @fk);

IF EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Products_IdDefaultCommessaResponsible' AND object_id=OBJECT_ID('Products'))
    DROP INDEX IX_Products_IdDefaultCommessaResponsible ON Products;

IF COL_LENGTH('Products','IdDefaultCommessaResponsible') IS NOT NULL
    ALTER TABLE Products DROP COLUMN IdDefaultCommessaResponsible;
");
        }
    }
}
