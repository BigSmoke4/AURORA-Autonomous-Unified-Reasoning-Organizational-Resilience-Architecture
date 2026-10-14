using Aurora.Shared.Kernel;

namespace Aurora.Web.Infrastructure.Events;

/// <summary>Writes one structured log entry per dispatched domain event, for operators and the telemetry pipeline.</summary>
public sealed class DomainEventLogHandler : IDomainEventHandler
{
    private readonly ILogger<DomainEventLogHandler> _logger;

    public DomainEventLogHandler(ILogger<DomainEventLogHandler> logger) => _logger = logger;

    public Task HandleAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        _logger.LogInformation(
            "Domain event {EventType} dispatched for organization {OrganizationId}, occurred at {OccurredOnUtc}.",
            domainEvent.GetType().Name,
            domainEvent.OrganizationId,
            domainEvent.OccurredOnUtc);
        return Task.CompletedTask;
    }
}
