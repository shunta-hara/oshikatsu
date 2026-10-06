using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OshikatsuApi.Migrations
{
    /// <inheritdoc />
    public partial class _20261006000003_ExtendCollectorJobLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "error_message",
                table: "collector_job_logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "failure_reason",
                table: "collector_job_logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "talent_id",
                table: "collector_job_logs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_collector_job_logs_talent_id",
                table: "collector_job_logs",
                column: "talent_id");

            migrationBuilder.AddForeignKey(
                name: "FK_collector_job_logs_talents_talent_id",
                table: "collector_job_logs",
                column: "talent_id",
                principalTable: "talents",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_collector_job_logs_talents_talent_id",
                table: "collector_job_logs");

            migrationBuilder.DropIndex(
                name: "ix_collector_job_logs_talent_id",
                table: "collector_job_logs");

            migrationBuilder.DropColumn(
                name: "error_message",
                table: "collector_job_logs");

            migrationBuilder.DropColumn(
                name: "failure_reason",
                table: "collector_job_logs");

            migrationBuilder.DropColumn(
                name: "talent_id",
                table: "collector_job_logs");
        }
    }
}
