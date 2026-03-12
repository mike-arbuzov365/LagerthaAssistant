using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LagerthaAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserMemoryEntries_IsActive_UpdatedAt",
                table: "UserMemoryEntries");

            migrationBuilder.DropIndex(
                name: "IX_UserMemoryEntries_Key",
                table: "UserMemoryEntries");

            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "UserMemoryEntries",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "unknown");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "UserMemoryEntries",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "anonymous");

            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "ConversationSessions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "unknown");

            migrationBuilder.AddColumn<string>(
                name: "ConversationId",
                table: "ConversationSessions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "default");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "ConversationSessions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "anonymous");

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
                name: "IX_ConversationSessions_Channel_UserId_ConversationId_UpdatedAt",
                table: "ConversationSessions",
                columns: new[] { "Channel", "UserId", "ConversationId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserMemoryEntries_Channel_UserId_IsActive_UpdatedAt",
                table: "UserMemoryEntries");

            migrationBuilder.DropIndex(
                name: "IX_UserMemoryEntries_Channel_UserId_Key",
                table: "UserMemoryEntries");

            migrationBuilder.DropIndex(
                name: "IX_ConversationSessions_Channel_UserId_ConversationId_UpdatedAt",
                table: "ConversationSessions");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "UserMemoryEntries");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "UserMemoryEntries");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "ConversationSessions");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "ConversationSessions");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ConversationSessions");

            migrationBuilder.CreateIndex(
                name: "IX_UserMemoryEntries_IsActive_UpdatedAt",
                table: "UserMemoryEntries",
                columns: new[] { "IsActive", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserMemoryEntries_Key",
                table: "UserMemoryEntries",
                column: "Key",
                unique: true);
        }
    }
}
