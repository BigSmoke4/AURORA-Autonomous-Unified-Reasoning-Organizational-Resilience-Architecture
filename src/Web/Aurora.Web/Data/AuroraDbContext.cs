using Aurora.Modules.AI.Domain;
using Aurora.Modules.Decisions.Domain;
using Aurora.Modules.DigitalTwin.Domain;
using Aurora.Modules.Graph.Domain;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.Organizations.Domain;
using Aurora.Modules.Risk.Domain;
using Aurora.Modules.Scenarios.Domain;
using Aurora.Modules.Simulation.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Data;

/// <summary>
/// The single composite EF Core DbContext for the whole modular monolith (spec section 40).
/// Each module owns its own IEntityTypeConfiguration classes (see each module's
/// Infrastructure/ folder); this context only wires them together via
/// ApplyConfigurationsFromAssembly, so no module reaches into another module's table directly.
/// AURORA remains a single deployable application (spec section 1) with one physical
/// PostgreSQL schema, while still keeping logical module ownership explicit in code.
/// </summary>
public class AuroraDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public AuroraDbContext(DbContextOptions<AuroraDbContext> options) : base(options) { }

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

    // Risk
    public DbSet<RiskRecord> RiskRecords => Set<RiskRecord>();

    // Decisions
    public DbSet<Decision> Decisions => Set<Decision>();

    // AI
    public DbSet<AiInvestigation> AiInvestigations => Set<AiInvestigation>();
    public DbSet<AiEvidence> AiEvidenceRecords => Set<AiEvidence>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Pick up every IEntityTypeConfiguration<T> from every module assembly that is
        // referenced by the Web project — this is how module ownership of schema stays
        // decentralized while the physical database remains one PostgreSQL instance.
        builder.ApplyConfigurationsFromAssembly(typeof(Organization).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(Supplier).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(GraphNode).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(Scenario).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(SimulationRun).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(RiskRecord).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(Decision).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(AiInvestigation).Assembly);

        // Identity tables get an "Aurora" prefix so they're visually grouped in the schema.
        builder.Entity<ApplicationUser>().ToTable("AuroraUsers");
        builder.Entity<ApplicationRole>().ToTable("AuroraRoles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("AuroraUserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("AuroraUserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("AuroraUserLogins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("AuroraRoleClaims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("AuroraUserTokens");
    }
}
