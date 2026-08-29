using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OshikatsuApi.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectorJobLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "collector_job_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    job_type = table.Column<string>(
                        type: "character varying(20)", maxLength: 20, nullable: false),
                    executed_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone", nullable: false),
                    success_count = table.Column<int>(type: "integer", nullable: false),
                    failure_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collector_job_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_collector_job_logs_job_type_executed_at",
                table: "collector_job_logs",
                columns: ["job_type", "executed_at"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "collector_job_logs");
        }
    }
}
