using CRM.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260707143000_UseContactNamesForUsers")]
    public partial class UseContactNamesForUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE c
                SET
                    c.Name = CASE WHEN NULLIF(LTRIM(RTRIM(c.Name)), '') IS NULL THEN COALESCE(NULLIF(LTRIM(RTRIM(u.Name)), ''), u.UserName, u.Email, 'Utente') ELSE c.Name END,
                    c.Surname = CASE WHEN NULLIF(LTRIM(RTRIM(c.Surname)), '') IS NULL THEN COALESCE(NULLIF(LTRIM(RTRIM(u.Surname)), ''), '-') ELSE c.Surname END,
                    c.Email = CASE WHEN NULLIF(LTRIM(RTRIM(c.Email)), '') IS NULL THEN u.Email ELSE c.Email END,
                    c.Phone = CASE WHEN NULLIF(LTRIM(RTRIM(c.Phone)), '') IS NULL THEN u.PhoneNumber ELSE c.Phone END
                FROM Contacts c
                INNER JOIN AspNetUsers u ON u.IdContact = c.Id;
                """);

            migrationBuilder.Sql(
                """
                DECLARE
                    @UserId nvarchar(450),
                    @IdCompany int,
                    @ContactName nvarchar(max),
                    @ContactSurname nvarchar(max),
                    @Email nvarchar(max),
                    @PhoneNumber nvarchar(max),
                    @ContactId int;

                DECLARE users_without_contact CURSOR LOCAL FAST_FORWARD FOR
                    SELECT
                        Id,
                        IdCompany,
                        COALESCE(NULLIF(LTRIM(RTRIM(Name)), ''), UserName, Email, 'Utente') AS ContactName,
                        COALESCE(NULLIF(LTRIM(RTRIM(Surname)), ''), '-') AS ContactSurname,
                        Email,
                        PhoneNumber
                    FROM AspNetUsers
                    WHERE IdContact IS NULL;

                OPEN users_without_contact;
                FETCH NEXT FROM users_without_contact INTO @UserId, @IdCompany, @ContactName, @ContactSurname, @Email, @PhoneNumber;

                WHILE @@FETCH_STATUS = 0
                BEGIN
                    INSERT INTO Contacts (IdCompany, Name, Surname, Email, Phone, Note)
                    VALUES (@IdCompany, @ContactName, @ContactSurname, @Email, @PhoneNumber, '');

                    SET @ContactId = CONVERT(int, SCOPE_IDENTITY());

                    UPDATE AspNetUsers
                    SET IdContact = @ContactId
                    WHERE Id = @UserId;

                    FETCH NEXT FROM users_without_contact INTO @UserId, @IdCompany, @ContactName, @ContactSurname, @Email, @PhoneNumber;
                END

                CLOSE users_without_contact;
                DEALLOCATE users_without_contact;
                """);

            migrationBuilder.DropColumn(
                name: "Name",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Surname",
                table: "AspNetUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Surname",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE u
                SET
                    u.Name = COALESCE(NULLIF(LTRIM(RTRIM(c.Name)), ''), u.UserName, u.Email, 'Utente'),
                    u.Surname = COALESCE(NULLIF(LTRIM(RTRIM(c.Surname)), ''), '-')
                FROM AspNetUsers u
                LEFT JOIN Contacts c ON c.Id = u.IdContact;
                """);
        }
    }
}
