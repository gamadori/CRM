using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailEngagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BounceReason",
                table: "EmailsSent",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BouncedAt",
                table: "EmailsSent",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClickCount",
                table: "EmailsSent",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "EmailsSent",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EngagementStatus",
                table: "EmailsSent",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastClickedAt",
                table: "EmailsSent",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastEventAt",
                table: "EmailsSent",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessageRef",
                table: "EmailsSent",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OpenCount",
                table: "EmailsSent",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenedAt",
                table: "EmailsSent",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmailEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdEmailSent = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailEvents_EmailsSent_IdEmailSent",
                        column: x => x.IdEmailSent,
                        principalTable: "EmailsSent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailsSent_MessageRef",
                table: "EmailsSent",
                column: "MessageRef");

            migrationBuilder.CreateIndex(
                name: "IX_EmailEvents_IdEmailSent",
                table: "EmailEvents",
                column: "IdEmailSent");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailEvents");

            migrationBuilder.DropIndex(
                name: "IX_EmailsSent_MessageRef",
                table: "EmailsSent");

            migrationBuilder.DropColumn(
                name: "BounceReason",
                table: "EmailsSent");

            migrationBuilder.DropColumn(
                name: "BouncedAt",
                table: "EmailsSent");

            migrationBuilder.DropColumn(
                name: "ClickCount",
                table: "EmailsSent");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "EmailsSent");

            migrationBuilder.DropColumn(
                name: "EngagementStatus",
                table: "EmailsSent");

            migrationBuilder.DropColumn(
                name: "LastClickedAt",
                table: "EmailsSent");

            migrationBuilder.DropColumn(
                name: "LastEventAt",
                table: "EmailsSent");

            migrationBuilder.DropColumn(
                name: "MessageRef",
                table: "EmailsSent");

            migrationBuilder.DropColumn(
                name: "OpenCount",
                table: "EmailsSent");

            migrationBuilder.DropColumn(
                name: "OpenedAt",
                table: "EmailsSent");
        }
    }
}
