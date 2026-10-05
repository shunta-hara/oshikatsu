using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OshikatsuApi.Migrations
{
    /// <inheritdoc />
    public partial class MakeOshiIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FEAT-040: collector が talentId のみで登録できるよう oshi_id を nullable に変更
            // FK 制約を一旦削除して列を変更してから再追加する

            migrationBuilder.DropForeignKey(
                name: "FK_news_items_oshis_oshi_id",
                table: "news_items");

            migrationBuilder.DropForeignKey(
                name: "FK_program_appearances_oshis_oshi_id",
                table: "program_appearances");

            migrationBuilder.AlterColumn<int>(
                name: "oshi_id",
                table: "news_items",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "oshi_id",
                table: "program_appearances",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_news_items_oshis_oshi_id",
                table: "news_items",
                column: "oshi_id",
                principalTable: "oshis",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_program_appearances_oshis_oshi_id",
                table: "program_appearances",
                column: "oshi_id",
                principalTable: "oshis",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_news_items_oshis_oshi_id",
                table: "news_items");

            migrationBuilder.DropForeignKey(
                name: "FK_program_appearances_oshis_oshi_id",
                table: "program_appearances");

            migrationBuilder.AlterColumn<int>(
                name: "oshi_id",
                table: "news_items",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "oshi_id",
                table: "program_appearances",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_news_items_oshis_oshi_id",
                table: "news_items",
                column: "oshi_id",
                principalTable: "oshis",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_program_appearances_oshis_oshi_id",
                table: "program_appearances",
                column: "oshi_id",
                principalTable: "oshis",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
