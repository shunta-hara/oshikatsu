using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OshikatsuApi.Migrations
{
    /// <inheritdoc />
    public partial class AddBookmarksNewsItemIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_bookmarks_news_item_id",
                table: "bookmarks",
                column: "news_item_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_bookmarks_news_item_id",
                table: "bookmarks");
        }
    }
}
