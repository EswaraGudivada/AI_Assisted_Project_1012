using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrlShortener.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ShortenedUrls",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ShortCode = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                OriginalUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ShortenedUrls", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ClickEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ShortenedUrlId = table.Column<Guid>(type: "TEXT", nullable: false),
                ClickedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClickEvents", x => x.Id);
                table.ForeignKey(
                    name: "FK_ClickEvents_ShortenedUrls",
                    column: x => x.ShortenedUrlId,
                    principalTable: "ShortenedUrls",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ClickedAtUtc",
            table: "ClickEvents",
            column: "ClickedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_ShortenedUrlId",
            table: "ClickEvents",
            column: "ShortenedUrlId");

        migrationBuilder.CreateIndex(
            name: "IX_ShortCode",
            table: "ShortenedUrls",
            column: "ShortCode",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ClickEvents");

        migrationBuilder.DropTable(
            name: "ShortenedUrls");
    }
}
