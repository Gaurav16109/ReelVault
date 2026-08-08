using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReelVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrichment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EnrichedAt",
                table: "SavedItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnrichmentData",
                table: "SavedItems",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnrichmentStatus",
                table: "SavedItems",
                type: "text",
                nullable: false,
                defaultValue: "NotEnriched");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnrichedAt",
                table: "SavedItems");

            migrationBuilder.DropColumn(
                name: "EnrichmentData",
                table: "SavedItems");

            migrationBuilder.DropColumn(
                name: "EnrichmentStatus",
                table: "SavedItems");
        }
    }
}
