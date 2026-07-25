using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReelVault.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: true),
                    SourceCaption = table.Column<string>(type: "text", nullable: true),
                    RawModelOutput = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UserNotes = table.Column<string>(type: "text", nullable: true),
                    CategoryData = table.Column<string>(type: "jsonb", nullable: false),
                    SavedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedItems_IsDeleted",
                table: "SavedItems",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_SavedItems_SavedAt",
                table: "SavedItems",
                column: "SavedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedItems");
        }
    }
}
