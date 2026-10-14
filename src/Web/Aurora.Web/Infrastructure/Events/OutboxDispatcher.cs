using Aurora.Web.Data;
using Aurora.Web.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aurora.Web.Infrastructure.Events;

/// <summary>Settings for the outbox dispatcher. Bound from the "Outbox" configuration section.</summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";
    public const int DefaultBatchSize = 50;
    public const int DefaultMaxAttempts = 5;
    public const int DefaultPollSeconds = 5;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(DefaultPollSeconds);

    public int BatchSize { get; set; } = DefaultBatchSize;

    public int MaxAttempts { get; set; } = DefaultMaxAttempts;
}

/// <summary>
/// Reads committed outbox messages in order and passes each one to every handler. A message is marked processed only
/// after all handlers succeed. A failure is recorded with the exception type and retried on a later poll, up to MaxAttempts.
/// </summary>
public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _clock;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        IServiceScopeFactory scopes,
        TimeProvider clock,
        IOptions<OutboxOptions> options,
        ILogger<OutboxDispatcher> logger)
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
                await DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The outbox batch could not be dispatched. It will be retried on the next poll.");
            }
        }
    }

    private async Task DispatchBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuroraDbContext>();
        var handlers = scope.ServiceProvider.GetServices<IDomainEventHandler>().ToList();

        var batch = await db.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null && m.Attempts < _options.MaxAttempts)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(_options.BatchSize)
            .ToListAsync(ct);
        if (batch.Count == 0) return;

        var now = _clock.GetUtcNow().UtcDateTime;
        foreach (var message in batch)
        {
            try
            {
                var domainEvent = DomainEventCatalog.Deserialize(message.EventType, message.Payload);
                foreach (var handler in handlers)
                {
                    await handler.HandleAsync(domainEvent, ct);
                }

                message.MarkProcessed(now);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.RecordFailure(ex.GetType().Name);
                _logger.LogWarning(
                    "Outbox message {MessageId} ({EventType}) failed on attempt {Attempt} with {ErrorType}.",
                    message.Id,
                    message.EventType,
                    message.Attempts,
                    ex.GetType().Name);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
