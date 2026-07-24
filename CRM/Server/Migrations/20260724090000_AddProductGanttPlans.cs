using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260724090000_AddProductGanttPlans")]
    public partial class AddProductGanttPlans : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('GanttPlans') IS NULL
BEGIN
    CREATE TABLE GanttPlans (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_GanttPlans PRIMARY KEY,
        Name nvarchar(max) NOT NULL,
        Description nvarchar(max) NULL,
        Kind int NOT NULL,
        State int NOT NULL,
        StartDate datetime2 NULL,
        EndDate datetime2 NULL,
        Progress int NOT NULL,
        CreatedAt datetime2 NOT NULL,
        IdUserCreate nvarchar(450) NULL,
        CONSTRAINT FK_GanttPlans_AspNetUsers_IdUserCreate FOREIGN KEY (IdUserCreate) REFERENCES AspNetUsers(Id)
    );
    CREATE INDEX IX_GanttPlans_IdUserCreate ON GanttPlans(IdUserCreate);
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('Products','IdGanttPlan') IS NULL
    ALTER TABLE Products ADD IdGanttPlan int NULL;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Products_IdGanttPlan' AND object_id=OBJECT_ID('Products'))
    CREATE INDEX IX_Products_IdGanttPlan ON Products(IdGanttPlan);
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_Products_GanttPlans_IdGanttPlan')
    ALTER TABLE Products ADD CONSTRAINT FK_Products_GanttPlans_IdGanttPlan FOREIGN KEY (IdGanttPlan) REFERENCES GanttPlans(Id) ON DELETE SET NULL;
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
               WHERE fkc.constraint_object_id=fk.object_id AND c.name='IdGanttPlan');
IF @fk IS NOT NULL EXEC('ALTER TABLE Products DROP CONSTRAINT ' + @fk);
IF EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Products_IdGanttPlan' AND object_id=OBJECT_ID('Products'))
    DROP INDEX IX_Products_IdGanttPlan ON Products;
IF COL_LENGTH('Products','IdGanttPlan') IS NOT NULL
    ALTER TABLE Products DROP COLUMN IdGanttPlan;

IF OBJECT_ID('GanttPlans') IS NOT NULL
    DROP TABLE GanttPlans;
");
        }
    }
}
