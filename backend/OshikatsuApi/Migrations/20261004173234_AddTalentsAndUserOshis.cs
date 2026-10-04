using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OshikatsuApi.Migrations
{
    /// <inheritdoc />
    public partial class AddTalentsAndUserOshis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "talent_id",
                table: "program_appearances",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "talent_id",
                table: "news_items",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "talents",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_talents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_oshis",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    talent_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_oshis", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_oshis_talents_talent_id",
                        column: x => x.talent_id,
                        principalTable: "talents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_oshis_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_program_appearances_talent_id",
                table: "program_appearances",
                column: "talent_id");

            migrationBuilder.CreateIndex(
                name: "ix_news_items_talent_id",
                table: "news_items",
                column: "talent_id");

            migrationBuilder.CreateIndex(
                name: "ix_talents_normalized_name",
                table: "talents",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_oshis_talent_id",
                table: "user_oshis",
                column: "talent_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_oshis_user_id",
                table: "user_oshis",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_oshis_user_id_talent_id",
                table: "user_oshis",
                columns: new[] { "user_id", "talent_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_news_items_talents_talent_id",
                table: "news_items",
                column: "talent_id",
                principalTable: "talents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_program_appearances_talents_talent_id",
                table: "program_appearances",
                column: "talent_id",
                principalTable: "talents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // ── データ移行 ──────────────────────────────────────────────────────
            // 1. oshis から talents を生成（normalized_name = 小文字 + 全空白除去）
            //    全角スペース（U+3000）も除去するためパターンに含む
            migrationBuilder.Sql("""
                INSERT INTO talents (display_name, normalized_name, created_at)
                SELECT DISTINCT ON (LOWER(REGEXP_REPLACE(name, '[\s　]+', '', 'g')))
                    name AS display_name,
                    LOWER(REGEXP_REPLACE(name, '[\s　]+', '', 'g')) AS normalized_name,
                    created_at
                FROM oshis
                ORDER BY LOWER(REGEXP_REPLACE(name, '[\s　]+', '', 'g')), created_at ASC
                ON CONFLICT (normalized_name) DO NOTHING;
                """);

            // 2. oshis から user_oshis を生成
            migrationBuilder.Sql("""
                INSERT INTO user_oshis (user_id, talent_id, created_at)
                SELECT o.user_id, t.id, o.created_at
                FROM oshis o
                JOIN talents t ON t.normalized_name = LOWER(REGEXP_REPLACE(o.name, '[\s　]+', '', 'g'));
                """);

            // 3. news_items.talent_id を設定
            migrationBuilder.Sql("""
                UPDATE news_items ni
                SET talent_id = t.id
                FROM oshis o
                JOIN talents t ON t.normalized_name = LOWER(REGEXP_REPLACE(o.name, '[\s　]+', '', 'g'))
                WHERE ni.oshi_id = o.id;
                """);

            // 4. program_appearances.talent_id を設定
            migrationBuilder.Sql("""
                UPDATE program_appearances pa
                SET talent_id = t.id
                FROM oshis o
                JOIN talents t ON t.normalized_name = LOWER(REGEXP_REPLACE(o.name, '[\s　]+', '', 'g'))
                WHERE pa.oshi_id = o.id;
                """);

            // 5. news_items 内の同一 talent_id + source_url 重複を削除（最古の1件を残す）
            migrationBuilder.Sql("""
                DELETE FROM news_items
                WHERE id NOT IN (
                    SELECT MIN(id)
                    FROM news_items
                    WHERE talent_id IS NOT NULL
                    GROUP BY talent_id, source_url
                )
                AND talent_id IS NOT NULL;
                """);

            // 6. program_appearances 内の同一 talent_id + source_url 重複を削除（最古の1件を残す）
            migrationBuilder.Sql("""
                DELETE FROM program_appearances
                WHERE id NOT IN (
                    SELECT MIN(id)
                    FROM program_appearances
                    WHERE talent_id IS NOT NULL
                    GROUP BY talent_id, source_url
                )
                AND talent_id IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_news_items_talents_talent_id",
                table: "news_items");

            migrationBuilder.DropForeignKey(
                name: "FK_program_appearances_talents_talent_id",
                table: "program_appearances");

            migrationBuilder.DropTable(
                name: "user_oshis");

            migrationBuilder.DropTable(
                name: "talents");

            migrationBuilder.DropIndex(
                name: "ix_program_appearances_talent_id",
                table: "program_appearances");

            migrationBuilder.DropIndex(
                name: "ix_news_items_talent_id",
                table: "news_items");

            migrationBuilder.DropColumn(
                name: "talent_id",
                table: "program_appearances");

            migrationBuilder.DropColumn(
                name: "talent_id",
                table: "news_items");
        }
    }
}
