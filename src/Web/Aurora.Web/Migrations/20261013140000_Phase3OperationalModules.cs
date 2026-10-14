using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aurora.Web.Migrations
{
    /// <summary>
    /// Phase 3 operational modules: workforce, finance, operations, technology, markets, assets, contracts,
    /// monitoring, alerts and supply chain. Each table is additive and tenant-scoped, so the migration
    /// creates new tables only and changes no existing data.
    /// Generated from one specification so the table definitions, the model snapshot and the designer agree.
    /// </summary>
    public partial class Phase3OperationalModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CriticalRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Criticality = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequiredHeadcount = table.Column<int>(type: "integer", nullable: false),
                    RoleTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriticalRoles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CriticalRoles_OrganizationId",
                table: "CriticalRoles",
                column: "OrganizationId");

            migrationBuilder.CreateTable(
                name: "CostCenters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualSpend = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AnnualBudget = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostCenters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CostCenters_OrganizationId",
                table: "CostCenters",
                column: "OrganizationId");

            migrationBuilder.CreateTable(
                name: "ProductionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualUnits = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DefectiveUnits = table.Column<int>(type: "integer", nullable: false),
                    FactoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlannedUnits = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRecords_OrganizationId",
                table: "ProductionRecords",
                column: "OrganizationId");

            migrationBuilder.CreateTable(
                name: "TechnologyPatchPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastPatchedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatchSlaDays = table.Column<int>(type: "integer", nullable: false),
                    TechnologyAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnologyPatchPolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TechnologyPatchPolicies_OrganizationId",
                table: "TechnologyPatchPolicies",
                column: "OrganizationId");

            migrationBuilder.CreateTable(
                name: "MarketObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DemandIndex = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Market = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ObservedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceIndex = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketObservations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketObservations_OrganizationId",
                table: "MarketObservations",
                column: "OrganizationId");

            migrationBuilder.CreateTable(
                name: "PhysicalAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AcquisitionCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InServiceOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResidualValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UsefulLifeYears = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalAssets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalAssets_OrganizationId",
                table: "PhysicalAssets",
                column: "OrganizationId");

            migrationBuilder.CreateTable(
                name: "SupplierContracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnnualValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Counterparty = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LatePenaltyPerDay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NoticePeriodDays = table.Column<int>(type: "integer", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    StartsOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierContracts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierContracts_OrganizationId",
                table: "SupplierContracts",
                column: "OrganizationId");

            migrationBuilder.CreateTable(
                name: "MetricDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MaximumThreshold = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MinimumThreshold = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricDefinitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetricDefinitions_OrganizationId",
                table: "MetricDefinitions",
                column: "OrganizationId");

            migrationBuilder.CreateTable(
                name: "MetricReadings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MetricDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ThresholdState = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Value = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricReadings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetricReadings_OrganizationId",
                table: "MetricReadings",
                column: "OrganizationId");

            migrationBuilder.CreateTable(
                name: "Alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RaisedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alerts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_OrganizationId",
                table: "Alerts",
                column: "OrganizationId");

            migrationBuilder.CreateTable(
                name: "InventoryPositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AverageDailyDemand = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OnHandUnits = table.Column<int>(type: "integer", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReplenishmentLeadTimeDays = table.Column<int>(type: "integer", nullable: false),
                    SafetyStockUnits = table.Column<int>(type: "integer", nullable: false),
                    Sku = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryPositions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryPositions_OrganizationId",
                table: "InventoryPositions",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryPositions");

            migrationBuilder.DropTable(
                name: "Alerts");

            migrationBuilder.DropTable(
                name: "MetricReadings");

            migrationBuilder.DropTable(
                name: "MetricDefinitions");

            migrationBuilder.DropTable(
                name: "SupplierContracts");

            migrationBuilder.DropTable(
                name: "PhysicalAssets");

            migrationBuilder.DropTable(
                name: "MarketObservations");

            migrationBuilder.DropTable(
                name: "TechnologyPatchPolicies");

            migrationBuilder.DropTable(
                name: "ProductionRecords");

            migrationBuilder.DropTable(
                name: "CostCenters");

            migrationBuilder.DropTable(
                name: "CriticalRoles");
        }
    }
}
