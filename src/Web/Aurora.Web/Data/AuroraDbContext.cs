using System.Reflection;
using Aurora.Modules.AI.Domain;
using Aurora.Modules.Analysis.Domain;
using Aurora.Modules.Audit.Domain;
using Aurora.Modules.Decisions.Domain;
using Aurora.Modules.DigitalTwin.Domain;
using Aurora.Modules.Graph.Domain;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.Organizations.Domain;
using Aurora.Modules.Risk.Domain;
using Aurora.Modules.Scenarios.Domain;
using Aurora.Modules.Simulation.Domain;
using Aurora.Shared.Kernel;
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
                entityType.FindProperty(nameof(Entity.Version))?.SetIsConcurrencyToken(true);
            }
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder builder) where TEntity : class, ITenantScoped =>
        builder.Entity<TEntity>().HasQueryFilter(e => e.OrganizationId == CurrentTenantId);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
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
            version.CurrentValue = (long)version.OriginalValue + 1L;
        }
    }
}
