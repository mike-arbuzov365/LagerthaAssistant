using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LagerthaAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "VocabularySyncJobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "VocabularyCards",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "UserMemoryEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "SystemPromptEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "StoreAliases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Meals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "MealIngredients",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "MealHistory",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "ItemAliases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "GroceryListItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "FoodItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "ConversationSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "ConversationIntentMetrics",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "ConversationHistoryEntries",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "VocabularySyncJobs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "VocabularyCards");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "UserMemoryEntries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SystemPromptEntries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "StoreAliases");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Meals");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MealIngredients");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MealHistory");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ItemAliases");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "GroceryListItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "FoodItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ConversationSessions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ConversationIntentMetrics");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ConversationHistoryEntries");
        }
    }
}
