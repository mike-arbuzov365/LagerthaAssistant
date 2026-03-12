using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LagerthaAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVocabularySqlIndexAndSyncQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VocabularyCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Word = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    NormalizedWord = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Meaning = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    Examples = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    PartOfSpeechMarker = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    DeckFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DeckPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    LastKnownRowNumber = table.Column<int>(type: "int", nullable: false),
                    StorageMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SyncStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LastSyncError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FirstSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SyncedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularyCards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VocabularySyncJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestedWord = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    AssistantReply = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    TargetDeckFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TargetDeckPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OverridePartOfSpeech = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    StorageMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularySyncJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VocabularyCardTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VocabularyCardId = table.Column<int>(type: "int", nullable: false),
                    TokenNormalized = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularyCardTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VocabularyCardTokens_VocabularyCards_VocabularyCardId",
                        column: x => x.VocabularyCardId,
                        principalTable: "VocabularyCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyCards_NormalizedWord_DeckFileName_StorageMode",
                table: "VocabularyCards",
                columns: new[] { "NormalizedWord", "DeckFileName", "StorageMode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyCards_StorageMode_LastSeenAtUtc",
                table: "VocabularyCards",
                columns: new[] { "StorageMode", "LastSeenAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyCards_SyncStatus_UpdatedAt",
                table: "VocabularyCards",
                columns: new[] { "SyncStatus", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyCardTokens_TokenNormalized",
                table: "VocabularyCardTokens",
                column: "TokenNormalized");

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyCardTokens_VocabularyCardId_TokenNormalized",
                table: "VocabularyCardTokens",
                columns: new[] { "VocabularyCardId", "TokenNormalized" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VocabularySyncJobs_Status_CreatedAtUtc",
                table: "VocabularySyncJobs",
                columns: new[] { "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VocabularyCardTokens");

            migrationBuilder.DropTable(
                name: "VocabularySyncJobs");

            migrationBuilder.DropTable(
                name: "VocabularyCards");
        }
    }
}
