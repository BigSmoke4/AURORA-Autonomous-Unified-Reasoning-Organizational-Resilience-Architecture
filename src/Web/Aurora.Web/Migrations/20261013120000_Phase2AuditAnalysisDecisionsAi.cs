using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aurora.Web.Migrations
{
    /// <summary>
    /// Phase 2 schema: audit trail, analysis records, decision provenance and prediction tracking,
    /// AI narrative provenance, and the optimistic concurrency token on every entity.
    /// Hand-written so that existing rows receive safe defaults.
    /// </summary>
    public partial class Phase2AuditAnalysisDecisionsAi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceKind",
                table: "Decisions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceAnalysisId",
                table: "Decisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProposedByUserId",
                table: "Decisions",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<string>(
                name: "ModelVersion",
                table: "Decisions",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PredictedRevenueImpact",
                table: "Decisions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ActualRevenueImpact",
                table: "Decisions",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PredictionErrorPct",
                table: "Decisions",
                type: "decimal(9,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScoringFactorsJson",
                table: "Decisions",
                type: "text",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.CreateIndex(
                name: "IX_Decisions_OrganizationId_ApprovalState",
                table: "Decisions",
                columns: new[] { "OrganizationId", "ApprovalState" });

            migrationBuilder.AddColumn<string>(
                name: "NarrativeSource",
                table: "AiInvestigations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Deterministic");

            migrationBuilder.AddColumn<string>(
                name: "ModelVersion",
                table: "AiInvestigations",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AnalysisRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ModelVersion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    InputJson = table.Column<string>(type: "text", nullable: false),
                    ResultJson = table.Column<string>(type: "text", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisRecords_OrganizationId_CreatedAtUtc",
                table: "AnalysisRecords",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Action = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_OrganizationId_CreatedAtUtc",
                table: "AuditEntries",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "AnalysisRecords");

            migrationBuilder.DropColumn(
                name: "ModelVersion",
                table: "AiInvestigations");

            migrationBuilder.DropColumn(
                name: "NarrativeSource",
                table: "AiInvestigations");

            migrationBuilder.DropIndex(
                name: "IX_Decisions_OrganizationId_ApprovalState",
                table: "Decisions");

            migrationBuilder.DropColumn(
                name: "ScoringFactorsJson",
                table: "Decisions");

            migrationBuilder.DropColumn(
                name: "PredictionErrorPct",
                table: "Decisions");

            migrationBuilder.DropColumn(
                name: "ActualRevenueImpact",
                table: "Decisions");

            migrationBuilder.DropColumn(
                name: "PredictedRevenueImpact",
                table: "Decisions");

            migrationBuilder.DropColumn(
                name: "ModelVersion",
                table: "Decisions");

            migrationBuilder.DropColumn(
                name: "ProposedByUserId",
                table: "Decisions");

            migrationBuilder.DropColumn(
                name: "SourceAnalysisId",
                table: "Decisions");

            migrationBuilder.DropColumn(
                name: "SourceKind",
                table: "Decisions");
        }
    }
}
