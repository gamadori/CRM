using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260706120000_UnifyContactUserAndTicketContact")]
    public partial class UnifyContactUserAndTicketContact : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdContact",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE u
                SET IdContact = c.Id
                FROM AspNetUsers u
                INNER JOIN Contacts c
                    ON c.Email = u.Email
                    AND (c.IdCompany = u.IdCompany OR (c.IdCompany IS NULL AND u.IdCompany IS NULL))
                WHERE u.IdContact IS NULL
                    AND u.Email IS NOT NULL
                    AND u.Email <> ''
                """);

            migrationBuilder.Sql("""
                DECLARE @NewUserContacts TABLE
                (
                    UserId nvarchar(450) NOT NULL,
                    ContactId int NOT NULL
                );

                MERGE Contacts AS target
                USING
                (
                    SELECT
                        u.Id AS UserId,
                        u.IdCompany,
                        COALESCE(NULLIF(u.Name, ''), NULLIF(u.UserName, ''), NULLIF(u.Email, ''), 'Utente') AS Name,
                        COALESCE(NULLIF(u.Surname, ''), '-') AS Surname,
                        u.Email,
                        u.PhoneNumber,
                        u.Note
                    FROM AspNetUsers u
                    WHERE u.IdContact IS NULL
                ) AS source
                ON 1 = 0
                WHEN NOT MATCHED THEN
                    INSERT (IdCompany, Name, Surname, Email, Mobile, Phone, Note, FacebookUrl, LinkedInUrl, TwitterUrl)
                    VALUES (source.IdCompany, source.Name, source.Surname, source.Email, NULL, source.PhoneNumber, source.Note, NULL, NULL, NULL)
                OUTPUT source.UserId, inserted.Id INTO @NewUserContacts;

                UPDATE u
                SET IdContact = n.ContactId
                FROM AspNetUsers u
                INNER JOIN @NewUserContacts n ON n.UserId = u.Id
                WHERE u.IdContact IS NULL
                """);

            migrationBuilder.Sql("""
                UPDATE t
                SET IdContact = u.IdContact
                FROM Tickets t
                INNER JOIN AspNetUsers u ON u.Id = t.IdUserCustomer
                WHERE t.IdContact IS NULL
                    AND t.IdUserCustomer IS NOT NULL
                    AND u.IdContact IS NOT NULL
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_IdContact",
                table: "AspNetUsers",
                column: "IdContact");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Contacts_IdContact",
                table: "AspNetUsers",
                column: "IdContact",
                principalTable: "Contacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.DropColumn(
                name: "IdUserCustomer",
                table: "Tickets");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdUserCustomer",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE t
                SET IdUserCustomer = u.Id
                FROM Tickets t
                INNER JOIN AspNetUsers u ON u.IdContact = t.IdContact
                WHERE t.IdUserCustomer IS NULL
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Contacts_IdContact",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_IdContact",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IdContact",
                table: "AspNetUsers");
        }
    }
}
