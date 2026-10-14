using Aurora.Web.Data;
using Aurora.Web.Data.Jobs;
using Aurora.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aurora.Web.Infrastructure.Jobs;

/// <summary>Settings for the job queue. Bound from the "BackgroundJobs" configuration section.</summary>
public sealed class BackgroundJobOptions
{
    public const string SectionName = "BackgroundJobs";
    public const int DefaultMaxAttempts = 3;
    public const int DefaultLeaseMinutes = 5;
    public const int DefaultRetrySeconds = 30;
    public const int DefaultPollSeconds = 2;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(DefaultPollSeconds);

    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(DefaultLeaseMinutes);

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(DefaultRetrySeconds);

    public int MaxAttempts { get; set; } = DefaultMaxAttempts;
}

/// <summary>
/// Runs queued jobs one at a time. Each job goes through three steps, each in its own scope: claim it, run its handler,
/// then record the outcome. A handler that fails cannot leave half-saved work to be committed with the outcome,
/// and a claim that loses a race is simply skipped.
/// </summary>
public sealed class BackgroundJobDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _clock;
    private readonly BackgroundJobOptions _options;
    private readonly ILogger<BackgroundJobDispatcher> _logger;

    public BackgroundJobDispatcher(IServiceScopeFactory scopes, TimeProvider clock, IOptions<BackgroundJobOptions> options,
        ILogger<BackgroundJobDispatcher> logger)
    {
        _scopes = scopes;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                while (await RunNextAsync(stoppingToken))
                {
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The background job loop failed. It will resume on the next poll.");
            }
        }
    }

    /// <summary>Claims and runs one due job. Returns false when no job is due.</summary>
    private async Task<bool> RunNextAsync(CancellationToken ct)
    {
        var claimed = await ClaimAsync(ct);
        if (claimed is null) return false;

        var (jobId, organizationId, kind, payload, attempt) = claimed.Value;
        string? summary = null;
        string? errorType = null;
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<JobTenantScope>().OrganizationId = organizationId;
            var handlers = scope.ServiceProvider.GetServices<IBackgroundJobHandler>().ToDictionary(h => h.Kind, StringComparer.Ordinal);
            if (!handlers.TryGetValue(kind, out var handler))
            {
                throw new InvalidOperationException($"No handler is registered for job kind '{kind}'.");
            }

            summary = await handler.HandleAsync(organizationId, payload, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errorType = ex.GetType().Name;
            _logger.LogWarning("Background job {JobId} ({Kind}) failed on attempt {Attempt} with {ErrorType}.",
                jobId, kind, attempt, errorType);
        }

        await FinishAsync(jobId, summary, errorType, ct);
        return true;
    }

    private async Task<(Guid JobId, Guid OrganizationId, string Kind, string Payload, int Attempt)?> ClaimAsync(CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuroraDbContext>();
        var now = _clock.GetUtcNow().UtcDateTime;

        var job = await db.BackgroundJobs
            .IgnoreQueryFilters()
            .Where(j => (j.Status == BackgroundJobStatus.Queued && j.AvailableAtUtc <= now)
                        || (j.Status == BackgroundJobStatus.Running && j.LockedUntilUtc <= now))
            .OrderBy(j => j.AvailableAtUtc)
            .FirstOrDefaultAsync(ct);
        if (job is null || !job.TryClaim(now, _options.LeaseDuration)) return null;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another worker claimed the job between our read and our write. The next poll looks again.
            return null;
        }

        return (job.Id, job.OrganizationId, job.Kind, job.PayloadJson, job.Attempts);
    }

    private async Task FinishAsync(Guid jobId, string? summary, string? errorType, CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuroraDbContext>();
        var job = await db.BackgroundJobs.IgnoreQueryFilters().FirstAsync(j => j.Id == jobId, ct);
        var now = _clock.GetUtcNow().UtcDateTime;

        if (summary is not null) job.Succeed(summary, now);
        else job.Fail(errorType ?? nameof(InvalidOperationException), now, _options.MaxAttempts, _options.RetryDelay);

        await db.SaveChangesAsync(ct);
    }
}
