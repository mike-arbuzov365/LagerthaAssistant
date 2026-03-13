using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LagerthaAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotionSyncCardStateAndPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NotionAttemptCount",
                table: "VocabularyCards",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NotionLastAttemptAtUtc",
                table: "VocabularyCards",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotionLastError",
                table: "VocabularyCards",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotionPageId",
                table: "VocabularyCards",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotionSyncStatus",
                table: "VocabularyCards",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NotionSyncedAtUtc",
                table: "VocabularyCards",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyCards_NotionSyncStatus_NotionLastAttemptAtUtc",
                table: "VocabularyCards",
                columns: new[] { "NotionSyncStatus", "NotionLastAttemptAtUtc" });

            migrationBuilder.Sql(
                "UPDATE [VocabularyCards] SET [NotionSyncStatus] = 'Pending' WHERE [NotionSyncStatus] IS NULL OR LTRIM(RTRIM([NotionSyncStatus])) = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VocabularyCards_NotionSyncStatus_NotionLastAttemptAtUtc",
                table: "VocabularyCards");

            migrationBuilder.DropColumn(
                name: "NotionAttemptCount",
                table: "VocabularyCards");

            migrationBuilder.DropColumn(
                name: "NotionLastAttemptAtUtc",
                table: "VocabularyCards");

            migrationBuilder.DropColumn(
                name: "NotionLastError",
                table: "VocabularyCards");

            migrationBuilder.DropColumn(
                name: "NotionPageId",
                table: "VocabularyCards");

            migrationBuilder.DropColumn(
                name: "NotionSyncStatus",
                table: "VocabularyCards");

            migrationBuilder.DropColumn(
                name: "NotionSyncedAtUtc",
                table: "VocabularyCards");
        }
    }
}
