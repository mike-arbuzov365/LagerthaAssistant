using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LagerthaAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationIntentMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationIntentMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MetricDateUtc = table.Column<DateTime>(type: "date", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AgentName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Intent = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsBatch = table.Column<bool>(type: "bit", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    TotalItems = table.Column<int>(type: "int", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationIntentMetrics", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationIntentMetrics_MetricDateUtc_Channel_AgentName_Intent_IsBatch",
                table: "ConversationIntentMetrics",
                columns: new[] { "MetricDateUtc", "Channel", "AgentName", "Intent", "IsBatch" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationIntentMetrics_MetricDateUtc_Channel_Count",
                table: "ConversationIntentMetrics",
                columns: new[] { "MetricDateUtc", "Channel", "Count" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationIntentMetrics");
        }
    }
}
