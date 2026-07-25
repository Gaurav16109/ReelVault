using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace ReelVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationAndSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Area",
                table: "SavedItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "SavedItems",
                type: "text",
                nullable: true);

            // Backfill Area/City for rows saved before these columns existed, from the same JSONB
            // fields the app already reads. Must run before SearchVector is added below, so its
            // initial computation reflects the backfilled values instead of empty strings.
            migrationBuilder.Sql(
                """
                UPDATE "SavedItems"
                SET "Area" = "CategoryData" ->> 'Area',
                    "City" = "CategoryData" ->> 'City'
                WHERE "Area" IS NULL AND "City" IS NULL;
                """);

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "SavedItems",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "to_tsvector('english',\n    coalesce(\"Title\", '') || ' ' ||\n    coalesce(\"Summary\", '') || ' ' ||\n    coalesce(\"Area\", '') || ' ' ||\n    coalesce(\"City\", '') || ' ' ||\n    coalesce(\"CategoryData\" ->> 'Name', '') || ' ' ||\n    coalesce(\"CategoryData\" ->> 'PlaceName', '') || ' ' ||\n    coalesce(\"CategoryData\" ->> 'Cuisine', '') || ' ' ||\n    coalesce(\"CategoryData\" ->> 'PlaceType', '') || ' ' ||\n    coalesce(\"CategoryData\" ->> 'MustTry', '') || ' ' ||\n    coalesce(\"CategoryData\" ->> 'Highlights', '')\n)",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedItems_Area",
                table: "SavedItems",
                column: "Area");

            migrationBuilder.CreateIndex(
                name: "IX_SavedItems_Category",
                table: "SavedItems",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_SavedItems_City",
                table: "SavedItems",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_SavedItems_SearchVector",
                table: "SavedItems",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SavedItems_Area",
                table: "SavedItems");

            migrationBuilder.DropIndex(
                name: "IX_SavedItems_Category",
                table: "SavedItems");

            migrationBuilder.DropIndex(
                name: "IX_SavedItems_City",
                table: "SavedItems");

            migrationBuilder.DropIndex(
                name: "IX_SavedItems_SearchVector",
                table: "SavedItems");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "SavedItems");

            migrationBuilder.DropColumn(
                name: "Area",
                table: "SavedItems");

            migrationBuilder.DropColumn(
                name: "City",
                table: "SavedItems");
        }
    }
}
