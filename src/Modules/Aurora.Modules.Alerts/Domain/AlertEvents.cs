using Aurora.Shared.Kernel;

namespace Aurora.Modules.Alerts.Domain;

/// <summary>Raised when a new alert opens for a metric or source.</summary>
public sealed record AlertRaised(
    Guid OrganizationId,
    Guid AlertId,
    string Title,
    AlertSeverity Severity,
    DateTime OccurredOnUtc) : IDomainEvent;

/// <summary>Raised when an alert is resolved.</summary>
public sealed record AlertResolved(
    Guid OrganizationId,
    Guid AlertId,
    DateTime OccurredOnUtc) : IDomainEvent;
