using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LagerthaAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFoodTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FoodItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NotionPageId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Store = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Quantity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastAddedToCartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotionSyncStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NotionAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NotionLastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NotionLastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotionSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotionUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Meals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NotionPageId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CaloriesPerServing = table.Column<int>(type: "integer", nullable: true),
                    ProteinGrams = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    CarbsGrams = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    FatGrams = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    PrepTimeMinutes = table.Column<int>(type: "integer", nullable: true),
                    DefaultServings = table.Column<int>(type: "integer", nullable: false),
                    NotionSyncStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NotionAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NotionLastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NotionLastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotionSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotionUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroceryListItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NotionPageId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Quantity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Store = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsBought = table.Column<bool>(type: "boolean", nullable: false),
                    FoodItemId = table.Column<int>(type: "integer", nullable: true),
                    NotionSyncStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NotionAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NotionLastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NotionLastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotionSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotionUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroceryListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroceryListItems_FoodItems_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MealHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MealId = table.Column<int>(type: "integer", nullable: false),
                    EatenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Servings = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: false),
                    CaloriesConsumed = table.Column<int>(type: "integer", nullable: true),
                    ProteinGrams = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    CarbsGrams = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    FatGrams = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealHistory_Meals_MealId",
                        column: x => x.MealId,
                        principalTable: "Meals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MealIngredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MealId = table.Column<int>(type: "integer", nullable: false),
                    FoodItemId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealIngredients_FoodItems_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MealIngredients_Meals_MealId",
                        column: x => x.MealId,
                        principalTable: "Meals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FoodItems_Name",
                table: "FoodItems",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_FoodItems_NotionPageId",
                table: "FoodItems",
                column: "NotionPageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FoodItems_NotionSyncStatus_NotionUpdatedAt",
                table: "FoodItems",
                columns: new[] { "NotionSyncStatus", "NotionUpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GroceryListItems_FoodItemId",
                table: "GroceryListItems",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "IX_GroceryListItems_IsBought",
                table: "GroceryListItems",
                column: "IsBought");

            migrationBuilder.CreateIndex(
                name: "IX_GroceryListItems_NotionPageId",
                table: "GroceryListItems",
                column: "NotionPageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroceryListItems_NotionSyncStatus_NotionUpdatedAt",
                table: "GroceryListItems",
                columns: new[] { "NotionSyncStatus", "NotionUpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MealHistory_EatenAt",
                table: "MealHistory",
                column: "EatenAt");

            migrationBuilder.CreateIndex(
                name: "IX_MealHistory_MealId",
                table: "MealHistory",
                column: "MealId");

            migrationBuilder.CreateIndex(
                name: "IX_MealIngredients_FoodItemId",
                table: "MealIngredients",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MealIngredients_MealId_FoodItemId",
                table: "MealIngredients",
                columns: new[] { "MealId", "FoodItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meals_Name",
                table: "Meals",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Meals_NotionPageId",
                table: "Meals",
                column: "NotionPageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meals_NotionSyncStatus_NotionUpdatedAt",
                table: "Meals",
                columns: new[] { "NotionSyncStatus", "NotionUpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroceryListItems");

            migrationBuilder.DropTable(
                name: "MealHistory");

            migrationBuilder.DropTable(
                name: "MealIngredients");

            migrationBuilder.DropTable(
                name: "FoodItems");

            migrationBuilder.DropTable(
                name: "Meals");
        }
    }
}
