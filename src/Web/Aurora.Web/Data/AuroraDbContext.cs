using System.Reflection;
using Aurora.Modules.AI.Domain;
using Aurora.Modules.Alerts.Domain;
using Aurora.Modules.Analysis.Domain;
using Aurora.Modules.Assets.Domain;
using Aurora.Modules.Audit.Domain;
using Aurora.Modules.Contracts.Domain;
using Aurora.Modules.Decisions.Domain;
using Aurora.Modules.DigitalTwin.Domain;
using Aurora.Modules.Finance.Domain;
using Aurora.Modules.Graph.Domain;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.Markets.Domain;
using Aurora.Modules.Monitoring.Domain;
using Aurora.Modules.Operations.Domain;
using Aurora.Modules.Organizations.Domain;
using Aurora.Modules.Risk.Domain;
using Aurora.Modules.Scenarios.Domain;
using Aurora.Modules.Simulation.Domain;
using Aurora.Modules.SupplyChain.Domain;
using Aurora.Modules.Technology.Domain;
using Aurora.Modules.Workforce.Domain;
using Aurora.Shared.Kernel;
using Aurora.Web.Data.Outbox;
using Aurora.Web.Infrastructure.Events;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Aurora.Web.Data;

/// <summary>
/// The single composite EF Core DbContext for the modular monolith (spec section 40). Each module
/// owns its IEntityTypeConfiguration classes; this context wires them together. It also enforces
/// two cross-cutting rules:
///   1. Tenant isolation. Every ITenantScoped entity gets a global query filter on the current
///      tenant. With no tenant (no signed-in user, or design-time tools) the filter matches nothing.
///   2. Optimistic concurrency. Every Entity.Version is a concurrency token, and it is incremented
///      on each update inside SaveChanges.
/// </summary>
public class AuroraDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private static readonly Guid NoTenant = Guid.Empty;

    private readonly ITenantContext _tenant;

    public AuroraDbContext(DbContextOptions<AuroraDbContext> options, ITenantContext tenant) : base(options)
    {
        _tenant = tenant;
    }

    private Guid CurrentTenantId => _tenant.OrganizationId ?? NoTenant;

    // Organizations
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<BusinessUnit> BusinessUnits => Set<BusinessUnit>();
    public DbSet<Department> Departments => Set<Department>();

    // DigitalTwin
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Factory> Factories => Set<Factory>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<TechnologyAsset> TechnologyAssets => Set<TechnologyAsset>();

    // Graph
    public DbSet<GraphNode> GraphNodes => Set<GraphNode>();
    public DbSet<GraphEdge> GraphEdges => Set<GraphEdge>();

    // Scenarios
    public DbSet<Scenario> Scenarios => Set<Scenario>();

    // Simulation
    public DbSet<SimulationRun> SimulationRuns => Set<SimulationRun>();
    public DbSet<SimulationStep> SimulationSteps => Set<SimulationStep>();
    public DbSet<SimulationResult> SimulationResults => Set<SimulationResult>();

    // Analysis (counterfactual, optimization and forecast records)
    public DbSet<AnalysisRecord> AnalysisRecords => Set<AnalysisRecord>();

    // Risk
    public DbSet<RiskRecord> RiskRecords => Set<RiskRecord>();

    // Decisions
    public DbSet<Decision> Decisions => Set<Decision>();

    // AI
    public DbSet<AiInvestigation> AiInvestigations => Set<AiInvestigation>();

    // Audit
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    // Phase 3 operational modules
    public DbSet<CriticalRole> CriticalRoles => Set<CriticalRole>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
    public DbSet<ProductionRecord> ProductionRecords => Set<ProductionRecord>();
    public DbSet<PatchPolicy> TechnologyPatchPolicies => Set<PatchPolicy>();
    public DbSet<MarketObservation> MarketObservations => Set<MarketObservation>();
    public DbSet<PhysicalAsset> PhysicalAssets => Set<PhysicalAsset>();
    public DbSet<SupplierContract> SupplierContracts => Set<SupplierContract>();
    public DbSet<MetricDefinition> MetricDefinitions => Set<MetricDefinition>();
    public DbSet<MetricReading> MetricReadings => Set<MetricReading>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<InventoryPosition> InventoryPositions => Set<InventoryPosition>();

    // Transactional outbox for domain events (see Data/Outbox and Infrastructure/Events).
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Module-owned mappings. Each module assembly contributes its own configurations.
        builder.ApplyConfigurationsFromAssembly(typeof(Organization).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(Supplier).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(GraphNode).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(Scenario).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(SimulationRun).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(RiskRecord).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(Decision).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(AiInvestigation).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(AnalysisRecord).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(AuditEntry).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(CriticalRole).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(CostCenter).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(ProductionRecord).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(PatchPolicy).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(MarketObservation).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(PhysicalAsset).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(SupplierContract).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(MetricDefinition).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(Alert).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(InventoryPosition).Assembly);
        builder.ApplyConfiguration(new OutboxMessageConfiguration());

        // Identity tables get an "Aurora" prefix so they're visually grouped in the schema.
        builder.Entity<ApplicationUser>().ToTable("AuroraUsers");
        builder.Entity<ApplicationRole>().ToTable("AuroraRoles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("AuroraUserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("AuroraUserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("AuroraUserLogins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("AuroraRoleClaims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("AuroraUserTokens");

        var entityTypes = builder.Model.GetEntityTypes().ToList();
        var applyFilter = typeof(AuroraDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var entityType in entityTypes)
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                applyFilter.MakeGenericMethod(entityType.ClrType).Invoke(this, new object[] { builder });
            }

            if (typeof(Entity).IsAssignableFrom(entityType.ClrType))
            {
                builder.Entity(entityType.ClrType).Property(nameof(Entity.Version)).IsConcurrencyToken();
            }
        }

        // AuditEntry is not ITenantScoped because platform-level sign-in events have no organization,
        // so its tenant filter is written explicitly. A null organization never equals a real tenant,
        // and a context with no tenant compares against NoTenant, which no row carries.
        builder.Entity<AuditEntry>().HasQueryFilter(e => e.OrganizationId == CurrentTenantId);
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder builder) where TEntity : class, ITenantScoped =>
        builder.Entity<TEntity>().HasQueryFilter(e => e.OrganizationId == CurrentTenantId);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StageDomainEvents();
        StampVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StageDomainEvents();
        StampVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Copies the events raised by aggregates into the outbox. The outbox rows are added before the save, so they
    /// commit in the same transaction as the change that raised them.
    /// </summary>
    private void StageDomainEvents()
    {
        var aggregates = ChangeTracker.Entries<AggregateRoot>().Select(entry => entry.Entity).ToList();
        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                OutboxMessages.Add(new OutboxMessage(
                    domainEvent.OrganizationId,
                    DomainEventCatalog.NameOf(domainEvent),
                    DomainEventCatalog.Serialize(domainEvent),
                    domainEvent.OccurredOnUtc));
            }

            aggregate.ClearDomainEvents();
        }
    }

    /// <summary>
    /// Advances the concurrency token of every modified entity. The WHERE clause keeps the value that
    /// was originally read, so a concurrent update raises DbUpdateConcurrencyException instead of
    /// silently overwriting it.
    /// </summary>
    private void StampVersions()
    {
        ChangeTracker.DetectChanges();
        foreach (EntityEntry<Entity> entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State != EntityState.Modified) continue;

            var version = entry.Property(nameof(Entity.Version));
            var original = (long?)version.OriginalValue
                ?? throw new InvalidOperationException("A modified entity has no original Version value.");
            version.CurrentValue = original + 1L;
        }
    }
}
