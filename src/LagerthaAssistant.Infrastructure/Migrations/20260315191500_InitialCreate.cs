using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LagerthaAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationIntentMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MetricDateUtc = table.Column<DateTime>(type: "date", nullable: false),
                    Channel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AgentName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Intent = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsBatch = table.Column<bool>(type: "boolean", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    TotalItems = table.Column<int>(type: "integer", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationIntentMetrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversationSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ConversationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemPromptEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PromptText = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemPromptEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemPromptProposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProposedPrompt = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AppliedSystemPromptEntryId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemPromptProposals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserMemoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Channel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMemoryEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VocabularyCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Word = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    NormalizedWord = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Meaning = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Examples = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    PartOfSpeechMarker = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    DeckFileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DeckPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    LastKnownRowNumber = table.Column<int>(type: "integer", nullable: false),
                    StorageMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SyncStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastSyncError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FirstSeenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SyncedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NotionSyncStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NotionPageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    NotionAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NotionLastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NotionLastAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NotionSyncedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularyCards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VocabularySyncJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestedWord = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    AssistantReply = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    TargetDeckFileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TargetDeckPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OverridePartOfSpeech = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    StorageMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularySyncJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversationHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConversationSessionId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Content = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationHistoryEntries_ConversationSessions_Conversatio~",
                        column: x => x.ConversationSessionId,
                        principalTable: "ConversationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VocabularyCardTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VocabularyCardId = table.Column<int>(type: "integer", nullable: false),
                    TokenNormalized = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
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
                name: "IX_ConversationHistoryEntries_ConversationSessionId_SentAtUtc",
                table: "ConversationHistoryEntries",
                columns: new[] { "ConversationSessionId", "SentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationIntentMetrics_MetricDateUtc_Channel_AgentName_I~",
                table: "ConversationIntentMetrics",
                columns: new[] { "MetricDateUtc", "Channel", "AgentName", "Intent", "IsBatch" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationIntentMetrics_MetricDateUtc_Channel_Count",
                table: "ConversationIntentMetrics",
                columns: new[] { "MetricDateUtc", "Channel", "Count" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSessions_Channel_UserId_ConversationId_UpdatedAt",
                table: "ConversationSessions",
                columns: new[] { "Channel", "UserId", "ConversationId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSessions_SessionKey",
                table: "ConversationSessions",
                column: "SessionKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemPromptEntries_IsActive",
                table: "SystemPromptEntries",
                column: "IsActive",
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_SystemPromptEntries_Version",
                table: "SystemPromptEntries",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemPromptProposals_Status_CreatedAtUtc",
                table: "SystemPromptProposals",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserMemoryEntries_Channel_UserId_IsActive_UpdatedAt",
                table: "UserMemoryEntries",
                columns: new[] { "Channel", "UserId", "IsActive", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserMemoryEntries_Channel_UserId_Key",
                table: "UserMemoryEntries",
                columns: new[] { "Channel", "UserId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyCards_NormalizedWord_DeckFileName_StorageMode",
                table: "VocabularyCards",
                columns: new[] { "NormalizedWord", "DeckFileName", "StorageMode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyCards_NotionSyncStatus_NotionLastAttemptAtUtc",
                table: "VocabularyCards",
                columns: new[] { "NotionSyncStatus", "NotionLastAttemptAtUtc" });

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
                name: "ConversationHistoryEntries");

            migrationBuilder.DropTable(
                name: "ConversationIntentMetrics");

            migrationBuilder.DropTable(
                name: "SystemPromptEntries");

            migrationBuilder.DropTable(
                name: "SystemPromptProposals");

            migrationBuilder.DropTable(
                name: "UserMemoryEntries");

            migrationBuilder.DropTable(
                name: "VocabularyCardTokens");

            migrationBuilder.DropTable(
                name: "VocabularySyncJobs");

            migrationBuilder.DropTable(
                name: "ConversationSessions");

            migrationBuilder.DropTable(
                name: "VocabularyCards");
        }
    }
}
