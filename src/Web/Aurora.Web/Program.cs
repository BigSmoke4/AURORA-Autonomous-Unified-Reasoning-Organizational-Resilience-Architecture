using Aurora.Modules.AI.Application;
using Aurora.Modules.AI.Contracts;
using Aurora.Modules.AI.Infrastructure;
using Aurora.Modules.DigitalTwin.Contracts;
using Aurora.Modules.Decisions.Domain;
using Aurora.Modules.Graph.Application;
using Aurora.Modules.Graph.Contracts;
using Aurora.Modules.Identity.Domain;
using Aurora.Modules.Organizations.Application;
using Aurora.Modules.Risk.Domain;
using Aurora.Modules.Scenarios.Application;
using Aurora.Modules.Scenarios.Contracts;
using Aurora.Modules.Simulation.Application;
using Aurora.Modules.Simulation.Contracts;
using Aurora.Shared.Events;
using Aurora.Shared.Observability;
using Aurora.Web.Data;
using Aurora.Web.Data.Seed;
using Aurora.Web.Hubs;
using Aurora.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
var isDevelopment = builder.Environment.IsDevelopment();

// ---------- Structured logging (spec section 39/68) ----------
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// ---------- Strongly typed options (spec section 92) ----------
builder.Services.AddSingleton(builder.Configuration.GetSection("Risk:ScoringWeights").Get<RiskScoringWeights>() ?? new RiskScoringWeights());
builder.Services.AddSingleton(builder.Configuration.GetSection("Decisions:ScoringWeights").Get<DecisionScoringWeights>() ?? new DecisionScoringWeights());

// ---------- PostgreSQL / EF Core ----------
// The connection string carries the database password, so it is never committed: set it with
// user-secrets (development) or the ConnectionStrings__AuroraDb environment variable (containers).
var connectionString = builder.Configuration.GetConnectionString("AuroraDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:AuroraDb is not configured. Set it with 'dotnet user-secrets' or the ConnectionStrings__AuroraDb environment variable.");
}

builder.Services.AddDbContext<AuroraDbContext>(options => options.UseNpgsql(
    connectionString,
    npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null)));

// ---------- ASP.NET Core Identity ----------
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequiredLength = 12;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireDigit = true;
    options.Password.RequiredUniqueChars = 4;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.SignIn.RequireConfirmedEmail = false;
})
    .AddEntityFrameworkStores<AuroraDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "aurora.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// ---------- Authorization policies from permission claims (spec section 43) ----------
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in AuroraPermissions.All)
    {
        options.AddPolicy(permission, policy =>
            policy.RequireAssertion(ctx =>
                ctx.User.HasClaim(c => c.Type == AuroraClaims.Permission && c.Value == permission) ||
                ctx.User.IsInRole(AuroraRoles.SystemAdministrator)));
    }
});

// ---------- Rate limiting (spec section 44) ----------
builder.Services.AddRateLimiter(RateLimitPolicies.Configure);

// ---------- MVC + Razor + SignalR ----------
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
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.SectionName));
builder.Services.AddSingleton<IAiProvider, NullAiProvider>(); // replaced by the LLM-backed provider when AI:Provider is configured
builder.Services.AddScoped<InvestigationOrchestrator>();

builder.Services.AddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();

// ---------- Health checks (spec section 69) ----------
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("postgresql", tags: new[] { "ready" })
    .AddCheck<AiProviderHealthCheck>("ai-provider", tags: new[] { "ready" });

// ---------- OpenTelemetry (spec section 68) ----------
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("Aurora.Web"))
    .WithTracing(tracing =>
    {
        tracing.AddSource(AuroraTelemetry.SourceName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
        if (isDevelopment) tracing.AddConsoleExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddMeter(AuroraTelemetry.MeterName)
            .AddAspNetCoreInstrumentation();
        if (isDevelopment) metrics.AddConsoleExporter();
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=CommandCenter}/{action=Index}/{id?}");
app.MapHub<SimulationHub>("/hubs/simulation");

// Liveness only proves the process is up; readiness also proves PostgreSQL is reachable (spec section 69).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapHealthChecks("/health");

await DbInitializer.SeedAsync(app.Services);

app.Run();

/// <summary>Exposed so integration tests can host the application with WebApplicationFactory.</summary>
public partial class Program { }
