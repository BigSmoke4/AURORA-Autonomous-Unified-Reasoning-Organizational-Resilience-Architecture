using Aurora.Modules.Alerts.Domain;
using Aurora.Modules.Decisions.Domain;
using Aurora.Shared.Kernel;
using Aurora.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Aurora.Web.Infrastructure.Events;

/// <summary>
/// Sends a short notification to the organization's SignalR group for decision and alert events. Events that are not
/// shown to users are skipped. Sending is idempotent: a repeated delivery only repeats the notice.
/// </summary>
public sealed class OrganizationNotificationHandler : IDomainEventHandler
{
    internal const string NotificationMethod = "domainEvent";

    private readonly IHubContext<OrganizationHub> _hub;

    public OrganizationNotificationHandler(IHubContext<OrganizationHub> hub) => _hub = hub;

    public async Task HandleAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        var summary = Summarize(domainEvent);
        if (summary is null) return;

        await _hub.Clients.Group(OrganizationHub.GroupName(domainEvent.OrganizationId)).SendAsync(
            NotificationMethod,
            new { type = domainEvent.GetType().Name, summary, occurredOnUtc = domainEvent.OccurredOnUtc },
            ct);
    }

    private static string? Summarize(IDomainEvent domainEvent) => domainEvent switch
    {
        DecisionStateChanged decision => $"Decision {decision.DecisionId:D}: {decision.Change}.",
        AlertRaised alert => $"Alert raised: {alert.Title}",
        AlertResolved alert => $"Alert {alert.AlertId:D} resolved.",
        _ => null,
    };
}
