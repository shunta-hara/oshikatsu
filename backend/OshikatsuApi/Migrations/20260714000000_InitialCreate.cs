using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OshikatsuApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── users ──────────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email = table.Column<string>(
                        type: "character varying(254)", maxLength: 254, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(
                        type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            // ── oshis ─────────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "oshis",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(
                        type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(
                        type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oshis", x => x.id);
                    table.ForeignKey(
                        name: "FK_oshis_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_oshis_user_id",
                table: "oshis",
                column: "user_id");

            // ── news_items ────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "news_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    oshi_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: true),
                    source_url = table.Column<string>(type: "text", nullable: false),
                    published_at = table.Column<DateTime>(
                        type: "timestamp with time zone", nullable: true),
                    is_published_at_estimated = table.Column<bool>(
                        type: "boolean", nullable: false),
                    collected_at = table.Column<DateTime>(
                        type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_news_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_news_items_oshis_oshi_id",
                        column: x => x.oshi_id,
                        principalTable: "oshis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_news_items_oshi_id",
                table: "news_items",
                column: "oshi_id");

            migrationBuilder.CreateIndex(
                name: "ix_news_items_source_url",
                table: "news_items",
                column: "source_url");

            // ── program_appearances ───────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "program_appearances",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    oshi_id = table.Column<int>(type: "integer", nullable: false),
                    program_name = table.Column<string>(type: "text", nullable: false),
                    broadcaster = table.Column<string>(type: "text", nullable: true),
                    aired_at = table.Column<DateTime>(
                        type: "timestamp with time zone", nullable: true),
                    is_aired_at_estimated = table.Column<bool>(
                        type: "boolean", nullable: false),
                    source_url = table.Column<string>(type: "text", nullable: false),
                    collected_at = table.Column<DateTime>(
                        type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_appearances", x => x.id);
                    table.ForeignKey(
                        name: "FK_program_appearances_oshis_oshi_id",
                        column: x => x.oshi_id,
                        principalTable: "oshis",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_program_appearances_oshi_id",
                table: "program_appearances",
                column: "oshi_id");

            migrationBuilder.CreateIndex(
                name: "ix_program_appearances_source_url",
                table: "program_appearances",
                column: "source_url");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "news_items");
            migrationBuilder.DropTable(name: "program_appearances");
            migrationBuilder.DropTable(name: "oshis");
            migrationBuilder.DropTable(name: "users");
        }
    }
}
