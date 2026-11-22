using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IndexSessionTokenHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AnonymousSession_TokenHash",
                table: "AnonymousSession",
                column: "TokenHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnonymousSession_TokenHash",
                table: "AnonymousSession");
        }
    }
}
