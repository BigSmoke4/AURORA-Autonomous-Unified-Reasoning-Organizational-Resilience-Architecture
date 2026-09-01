using System.Security.Claims;
using Aurora.Modules.AI.Application;
using Aurora.Modules.AI.Contracts;
using Aurora.Modules.AI.Infrastructure;
using Aurora.Modules.DigitalTwin.Contracts;
using Aurora.Modules.Graph.Application;
using Aurora.Modules.Graph.Contracts;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.Organizations.Application;
using Aurora.Modules.Risk.Domain;
using Aurora.Modules.Decisions.Domain;
using Aurora.Modules.Scenarios.Application;
using Aurora.Modules.Scenarios.Contracts;
using Aurora.Modules.Simulation.Application;
using Aurora.Modules.Simulation.Contracts;
using Aurora.Shared.Events;
using Aurora.Web.Data;
using Aurora.Web.Data.Seed;
using Aurora.Web.Hubs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---------- Structured logging (spec section: structured logging / observability) ----------
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// ---------- Strongly typed options (spec section 92 — no scattered config strings) ----------
var riskWeights = builder.Configuration.GetSection("Risk:ScoringWeights").Get<RiskScoringWeights>() ?? new RiskScoringWeights();
var decisionWeights = builder.Configuration.GetSection("Decisions:ScoringWeights").Get<DecisionScoringWeights>() ?? new DecisionScoringWeights();
builder.Services.AddSingleton(riskWeights);
builder.Services.AddSingleton(decisionWeights);

// ---------- PostgreSQL / EF Core ----------
var connectionString = builder.Configuration.GetConnectionString("AuroraDb")
    ?? throw new InvalidOperationException("Connection string 'AuroraDb' is not configured.");
builder.Services.AddDbContext<AuroraDbContext>(options => options.UseNpgsql(connectionString));

// ---------- ASP.NET Core Identity ----------
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequiredLength = 10;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.SignIn.RequireConfirmedEmail = false;
})
    .AddEntityFrameworkStores<AuroraDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// ---------- Authorization policies from permission claims (spec section 43) ----------
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in new[]
    {
        AuroraPermissions.GraphRead, AuroraPermissions.GraphWrite, AuroraPermissions.ScenarioRead,
        AuroraPermissions.ScenarioCreate, AuroraPermissions.ScenarioRun, AuroraPermissions.SimulationRead,
        AuroraPermissions.SimulationRun, AuroraPermissions.OptimizationRun, AuroraPermissions.AiInvestigate,
        AuroraPermissions.AiApprove, AuroraPermissions.DecisionRead, AuroraPermissions.DecisionApprove,
        AuroraPermissions.DecisionExecute, AuroraPermissions.ReportsRead
    })
    {
        options.AddPolicy(permission, policy =>
            policy.RequireAssertion(ctx =>
                ctx.User.HasClaim(c => c.Type == "aurora:permission" && c.Value == permission) ||
                ctx.User.IsInRole(AuroraRoles.SystemAdministrator)));
    }
});

// ---------- MVC + Razor ----------
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Aurora.Web.Models.CurrentOrganizationAccessor>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authentication.IClaimsTransformation, Aurora.Web.Models.AuroraClaimsTransformation>();

// ---------- Module contracts -> Web infrastructure implementations ----------
builder.Services.AddScoped<IGraphRepository, GraphRepository>();
builder.Services.AddScoped<IGraphProjectionService, GraphProjectionService>();
builder.Services.AddSingleton<GraphTraversalService>();

builder.Services.AddScoped<IScenarioRepository, ScenarioRepository>();
builder.Services.AddSingleton<NaturalLanguageScenarioExtractor>();

builder.Services.AddScoped<ISimulationRepository, SimulationRepository>();
builder.Services.AddSingleton<SimulationEngine>();
builder.Services.AddScoped<ISimulationProgressNotifier, SignalRSimulationProgressNotifier>();

builder.Services.AddScoped<IDigitalTwinReadService, DigitalTwinReadService>();
builder.Services.AddScoped<IOrganizationDirectory, OrganizationDirectory>();

builder.Services.AddScoped<IAiInvestigationRepository, AiInvestigationRepository>();
builder.Services.AddSingleton<IAiProvider, NullAiProvider>(); // swap for a real LLM-backed IAiProvider once AI:ApiKey is configured
builder.Services.AddScoped<InvestigationOrchestrator>();

builder.Services.AddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();

// ---------- Health checks (spec section 69) ----------
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql");

// ---------- OpenTelemetry (spec section 68) ----------
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=CommandCenter}/{action=Index}/{id?}");
app.MapHub<SimulationHub>("/hubs/simulation");
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

await DbInitializer.SeedAsync(app.Services);

app.Run();
