using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessageSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatMessage_ConversationId",
                table: "ChatMessage");

            migrationBuilder.AddColumn<long>(
                name: "Sequence",
                table: "ChatMessage",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            // Preserve existing history, numbering each conversation independently.
            migrationBuilder.Sql("""
                WITH OrderedMessages AS (
                    SELECT "Id", ROW_NUMBER() OVER (
                        PARTITION BY "ConversationId" ORDER BY "CreatedAt", "Id"
                    ) AS SequenceNumber
                    FROM "ChatMessage"
                )
                UPDATE "ChatMessage"
                SET "Sequence" = (
                    SELECT SequenceNumber FROM OrderedMessages
                    WHERE OrderedMessages."Id" = "ChatMessage"."Id"
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessage_ConversationId_Sequence",
                table: "ChatMessage",
                columns: new[] { "ConversationId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatMessage_ConversationId_Sequence",
                table: "ChatMessage");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "ChatMessage");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessage_ConversationId",
                table: "ChatMessage",
                column: "ConversationId");
        }
    }
}
