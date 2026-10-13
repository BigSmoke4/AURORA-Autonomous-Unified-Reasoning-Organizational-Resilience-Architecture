using Aurora.Modules.DigitalTwin.Domain;
using Aurora.Modules.Graph.Domain;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.Organizations.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Web.Data.Seed;

/// <summary>
/// Builds a realistic demo enterprise with a deliberate single-source dependency (spec section 81/82) so the
/// Command Center, Graph, Risk, and Simulation features have real data to operate on the first
/// time AURORA starts. Idempotent: does nothing if any organization already exists.
/// </summary>
public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuroraDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AuroraDbContextSeedMarker>>();

        await db.Database.MigrateAsync();

        await SeedRolesAsync(roleManager);
        var adminUser = await SeedAdminUserAsync(userManager, configuration["Seed:AdminPassword"], logger);

        if (await db.Organizations.AnyAsync())
        {
            logger.LogInformation("AURORA seed data already present; skipping.");
            return;
        }

        logger.LogInformation("Seeding AURORA demo enterprise: Meridian Industrial Group...");

        var org = new Organization("Meridian Industrial Group", "Industrial Manufacturing", "Columbus, OH, USA");
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        if (adminUser is not null && adminUser.OrganizationId is null)
        {
            adminUser.OrganizationId = org.Id;
            await userManager.UpdateAsync(adminUser);
        }

        // --- Business Units & Departments ---
        var manufacturingBu = new BusinessUnit(org.Id, "Manufacturing Operations");
        var technologyBu = new BusinessUnit(org.Id, "Technology");
        var commercialBu = new BusinessUnit(org.Id, "Commercial");
        db.BusinessUnits.AddRange(manufacturingBu, technologyBu, commercialBu);
        await db.SaveChangesAsync();

        var productionDept = new Department(org.Id, manufacturingBu.Id, "Production");
        var procurementDept = new Department(org.Id, manufacturingBu.Id, "Procurement");
        var itDept = new Department(org.Id, technologyBu.Id, "IT Infrastructure");
        var salesDept = new Department(org.Id, commercialBu.Id, "Sales");
        db.Departments.AddRange(productionDept, procurementDept, itDept, salesDept);
        await db.SaveChangesAsync();

        // --- Employees ---
        var employeeNames = new[]
        {
            ("Maria Chen", "VP Manufacturing", productionDept.Id),
            ("James Okafor", "Plant Operations Manager", productionDept.Id),
            ("Priya Natarajan", "Procurement Director", procurementDept.Id),
            ("Tom Whitfield", "Supply Chain Analyst", procurementDept.Id),
            ("Lena Kowalski", "IT Infrastructure Lead", itDept.Id),
            ("David Kim", "Cloud Platform Engineer", itDept.Id),
            ("Sarah Novak", "VP Sales", salesDept.Id),
            ("Marcus Webb", "Account Director", salesDept.Id),
        };
        var employees = employeeNames.Select(e => new Employee(org.Id, e.Item1, e.Item2, e.Item3)).ToList();
        db.Employees.AddRange(employees);
        await db.SaveChangesAsync();

        // --- Suppliers (intentionally concentrated: Northbridge is the only supplier of its type for Factory Alpha) ---
        var suppliers = new List<Supplier>
        {
            new(org.Id, "Northbridge Components", "Precision Components", capacityUtilizationPct: 92, criticality: 5, annualRevenueExposure: 42_000_000m, location: "Shenzhen, CN"),
            new(org.Id, "Halcyon Metals", "Raw Materials", capacityUtilizationPct: 78, criticality: 4, annualRevenueExposure: 18_000_000m, location: "Pittsburgh, PA"),
            new(org.Id, "Vantage Electronics", "Electronic Subassemblies", capacityUtilizationPct: 85, criticality: 4, annualRevenueExposure: 22_000_000m, location: "Taipei, TW"),
            new(org.Id, "Coastal Logistics Partners", "Freight & Transport", capacityUtilizationPct: 70, criticality: 3, annualRevenueExposure: 9_000_000m, location: "Long Beach, CA"),
            new(org.Id, "Ferra Polymers", "Plastics & Resins", capacityUtilizationPct: 65, criticality: 3, annualRevenueExposure: 7_500_000m, location: "Houston, TX"),
            new(org.Id, "BrightWeld Fasteners", "Hardware", capacityUtilizationPct: 60, criticality: 2, annualRevenueExposure: 3_000_000m, location: "Cleveland, OH"),
        };
        db.Suppliers.AddRange(suppliers);
        await db.SaveChangesAsync();

        // --- Factories ---
        var factories = new List<Factory>
        {
            new(org.Id, "Factory Alpha (Columbus)", "Columbus, OH", capacityUtilizationPct: 88, criticality: 5, annualRevenueExposure: 55_000_000m),
            new(org.Id, "Factory Beta (Monterrey)", "Monterrey, MX", capacityUtilizationPct: 74, criticality: 4, annualRevenueExposure: 30_000_000m),
            new(org.Id, "Factory Gamma (Katowice)", "Katowice, PL", capacityUtilizationPct: 60, criticality: 3, annualRevenueExposure: 15_000_000m),
        };
        db.Factories.AddRange(factories);
        await db.SaveChangesAsync();

        // --- Warehouses ---
        var warehouses = new List<Warehouse>
        {
            new(org.Id, "Central Distribution Center", "Columbus, OH", 82),
            new(org.Id, "West Coast Warehouse", "Long Beach, CA", 68),
        };
        db.Warehouses.AddRange(warehouses);
        await db.SaveChangesAsync();

        // --- Products ---
        var products = new List<Product>
        {
            new(org.Id, "Industrial Actuator Series 7", "IAC-7000", 38_000_000m),
            new(org.Id, "Precision Hydraulic Pump", "PHP-450", 21_000_000m),
            new(org.Id, "Modular Control Unit", "MCU-220", 14_000_000m),
        };
        db.Products.AddRange(products);
        await db.SaveChangesAsync();

        // --- Customers ---
        var customers = new List<Customer>
        {
            new(org.Id, "Continental Heavy Equipment", "OEM", 26_000_000m),
            new(org.Id, "Apex Rail Systems", "Transportation", 17_000_000m),
            new(org.Id, "Summit Energy Group", "Energy", 12_000_000m),
        };
        db.Customers.AddRange(customers);
        await db.SaveChangesAsync();

        // --- Technology Assets ---
        var techAssets = new List<TechnologyAsset>
        {
            new(org.Id, "MES Production App", TechnologyAssetType.Application, 5),
            new(org.Id, "Production Database Cluster", TechnologyAssetType.Database, 5),
            new(org.Id, "Primary Cloud Region (us-east)", TechnologyAssetType.CloudResource, 4),
            new(org.Id, "Order Management System", TechnologyAssetType.Application, 3),
        };
        db.TechnologyAssets.AddRange(techAssets);
        await db.SaveChangesAsync();

        // --- Graph projection: one node per entity ---
        var nodeByEntity = new Dictionary<Guid, GraphNode>();

        GraphNode AddNode(GraphNodeType type, Guid entityId, string name, int criticality, decimal revenueExposure, string status = "Healthy")
        {
            var node = new GraphNode(org.Id, type, entityId, name, criticality, revenueExposure, status);
            db.GraphNodes.Add(node);
            nodeByEntity[entityId] = node;
            return node;
        }

        foreach (var s in suppliers)
            AddNode(GraphNodeType.Supplier, s.Id, s.Name, s.Criticality, s.AnnualRevenueExposure,
                s.CapacityUtilizationPct < 70 ? "Warning" : "Healthy");
        foreach (var f in factories)
            AddNode(GraphNodeType.Factory, f.Id, f.Name, f.Criticality, f.AnnualRevenueExposure);
        foreach (var w in warehouses)
            AddNode(GraphNodeType.Warehouse, w.Id, w.Name, 3, 0m);
        foreach (var p in products)
            AddNode(GraphNodeType.Product, p.Id, p.Name, 4, p.AnnualRevenueExposure);
        foreach (var c in customers)
            AddNode(GraphNodeType.Customer, c.Id, c.Name, 4, c.AnnualRevenueExposure);
        foreach (var t in techAssets)
            AddNode(GraphNodeType.TechnologyAsset, t.Id, t.Name, t.Criticality, 0m);
        foreach (var e in employees)
            AddNode(GraphNodeType.Employee, e.Id, e.FullName, 2, 0m);

        await db.SaveChangesAsync();

        // --- Graph edges: intentional dependency web (spec section 81 "intentional
        // dependencies and vulnerabilities") ---
        void AddEdge(Guid sourceEntityId, Guid targetEntityId, GraphRelationshipType type, decimal strength, int criticality)
        {
            var source = nodeByEntity[sourceEntityId];
            var target = nodeByEntity[targetEntityId];
            db.GraphEdges.Add(new GraphEdge(org.Id, source.Id, target.Id, type, strength, criticality));
        }

        // Northbridge Components (critical, concentrated) supplies Factory Alpha almost exclusively.
        AddEdge(suppliers[0].Id, factories[0].Id, GraphRelationshipType.Supplies, 0.9m, 5); // Northbridge -> Alpha
        AddEdge(suppliers[1].Id, factories[0].Id, GraphRelationshipType.Supplies, 0.5m, 3); // Halcyon -> Alpha
        AddEdge(suppliers[1].Id, factories[1].Id, GraphRelationshipType.Supplies, 0.6m, 3); // Halcyon -> Beta
        AddEdge(suppliers[2].Id, factories[0].Id, GraphRelationshipType.Supplies, 0.7m, 4); // Vantage -> Alpha
        AddEdge(suppliers[2].Id, factories[1].Id, GraphRelationshipType.Supplies, 0.4m, 3); // Vantage -> Beta
        AddEdge(suppliers[3].Id, factories[0].Id, GraphRelationshipType.Supplies, 0.3m, 2); // Coastal Logistics -> Alpha
        AddEdge(suppliers[4].Id, factories[1].Id, GraphRelationshipType.Supplies, 0.5m, 2); // Ferra Polymers -> Beta
        AddEdge(suppliers[4].Id, factories[2].Id, GraphRelationshipType.Supplies, 0.5m, 2); // Ferra Polymers -> Gamma
        AddEdge(suppliers[5].Id, factories[2].Id, GraphRelationshipType.Supplies, 0.4m, 2); // BrightWeld -> Gamma

        // Factories produce products.
        AddEdge(factories[0].Id, products[0].Id, GraphRelationshipType.Produces, 0.8m, 5); // Alpha -> Actuator
        AddEdge(factories[0].Id, products[1].Id, GraphRelationshipType.Produces, 0.5m, 4); // Alpha -> Hydraulic Pump
        AddEdge(factories[1].Id, products[1].Id, GraphRelationshipType.Produces, 0.6m, 4); // Beta -> Hydraulic Pump
        AddEdge(factories[1].Id, products[2].Id, GraphRelationshipType.Produces, 0.7m, 3); // Beta -> MCU
        AddEdge(factories[2].Id, products[2].Id, GraphRelationshipType.Produces, 0.5m, 3); // Gamma -> MCU

        // Warehouses store products.
        AddEdge(products[0].Id, warehouses[0].Id, GraphRelationshipType.Stores, 0.7m, 3);
        AddEdge(products[1].Id, warehouses[0].Id, GraphRelationshipType.Stores, 0.6m, 3);
        AddEdge(products[2].Id, warehouses[1].Id, GraphRelationshipType.Stores, 0.6m, 2);

        // Products serve customers.
        AddEdge(products[0].Id, customers[0].Id, GraphRelationshipType.Serves, 0.8m, 5); // Actuator -> Continental
        AddEdge(products[1].Id, customers[1].Id, GraphRelationshipType.Serves, 0.7m, 4); // Hydraulic Pump -> Apex Rail
        AddEdge(products[1].Id, customers[2].Id, GraphRelationshipType.Serves, 0.4m, 3); // Hydraulic Pump -> Summit Energy
        AddEdge(products[2].Id, customers[2].Id, GraphRelationshipType.Serves, 0.6m, 3); // MCU -> Summit Energy

        // Technology dependency chain: MES App depends on Database depends on Cloud Region.
        AddEdge(techAssets[0].Id, techAssets[1].Id, GraphRelationshipType.DependsOn, 0.9m, 5); // MES -> DB
        AddEdge(techAssets[1].Id, techAssets[2].Id, GraphRelationshipType.DependsOn, 0.8m, 4); // DB -> Cloud Region
        AddEdge(techAssets[3].Id, techAssets[1].Id, GraphRelationshipType.DependsOn, 0.6m, 3); // OMS -> DB
        AddEdge(factories[0].Id, techAssets[0].Id, GraphRelationshipType.Uses, 0.7m, 4); // Alpha uses MES
        AddEdge(techAssets[3].Id, customers[0].Id, GraphRelationshipType.Affects, 0.3m, 2); // OMS affects order fulfillment for Continental

        // Employees work for departments / report to managers (kept light — a few illustrative edges).
        AddEdge(employees[1].Id, employees[0].Id, GraphRelationshipType.ReportsTo, 0.9m, 2); // Ops Manager -> VP Manufacturing
        AddEdge(employees[3].Id, employees[2].Id, GraphRelationshipType.ReportsTo, 0.9m, 2); // Analyst -> Procurement Director
        AddEdge(employees[0].Id, factories[0].Id, GraphRelationshipType.WorksFor, 0.5m, 3);

        await db.SaveChangesAsync();

        logger.LogInformation(
            "AURORA seed complete: organization {Org}, {NodeCount} graph nodes, {EdgeCount} graph edges.",
            org.Name, nodeByEntity.Count, await db.GraphEdges.IgnoreQueryFilters().CountAsync(e => e.OrganizationId == org.Id));
    }

    private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        foreach (var roleName in AuroraRoles.All)
        {
            if (await roleManager.RoleExistsAsync(roleName)) continue;

            var role = new ApplicationRole(roleName);
            await roleManager.CreateAsync(role);

            if (AuroraPermissions.DefaultRolePermissions.TryGetValue(roleName, out var permissions))
            {
                foreach (var permission in permissions)
                    await roleManager.AddClaimAsync(role, new System.Security.Claims.Claim("aurora:permission", permission));
            }
        }
    }

    /// <summary>
    /// Creates the bootstrap administrator only when a password is supplied through configuration
    /// (Seed:AdminPassword, from user-secrets or the environment). No credential is ever hard-coded.
    /// </summary>
    private static async Task<ApplicationUser?> SeedAdminUserAsync(
        UserManager<ApplicationUser> userManager, string? adminPassword, ILogger logger)
    {
        const string adminEmail = "admin@aurora.local";
        var existing = await userManager.FindByEmailAsync(adminEmail);
        if (existing is not null) return existing;

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("No bootstrap administrator exists and Seed:AdminPassword is not configured; skipping admin creation.");
            return null;
        }

        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = "AURORA System Administrator",
            JobTitle = "System Administrator",
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, adminPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException("Failed to seed admin user: " + string.Join(", ", result.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(admin, AuroraRoles.SystemAdministrator);
        return admin;
    }
}

/// <summary>Dummy type solely so DbInitializer can obtain a well-scoped ILogger without pulling
/// in a dependency on the Program class.</summary>
public sealed class AuroraDbContextSeedMarker { }
