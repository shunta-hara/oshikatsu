using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OshikatsuApi.Migrations
{
    /// <inheritdoc />
    public partial class AddTalentSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "source_type",
                table: "news_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "talent_sources",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    talent_id = table.Column<int>(type: "integer", nullable: false),
                    source_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_talent_sources", x => x.id);
                    table.ForeignKey(
                        name: "FK_talent_sources_talents_talent_id",
                        column: x => x.talent_id,
                        principalTable: "talents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_talent_sources_talent_id",
                table: "talent_sources",
                column: "talent_id");

            migrationBuilder.CreateIndex(
                name: "ix_talent_sources_talent_id_source_type_source_id",
                table: "talent_sources",
                columns: new[] { "talent_id", "source_type", "source_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "talent_sources");

            migrationBuilder.DropColumn(
                name: "source_type",
                table: "news_items");
        }
    }
}
