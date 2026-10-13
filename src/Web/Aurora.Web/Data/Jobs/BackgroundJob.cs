using Aurora.Shared.Kernel;

namespace Aurora.Web.Data.Jobs;

public enum BackgroundJobStatus
{
    Queued = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
}

/// <summary>
/// A unit of work run outside the request, such as a feed ingestion. A job runs at least once. If it fails it is retried
/// after a delay, up to a maximum number of attempts. A job whose lease expires while it is running is claimed again,
/// so handlers must be idempotent. Jobs are tenant-scoped, so pages read them through the central tenant filter. The
/// dispatcher, which serves every organization, reads them with the filter explicitly ignored.
/// </summary>
public sealed class BackgroundJob : Entity, ITenantScoped
{
    public const int KindMaxLength = 100;
    public const int PayloadMaxLength = 4000;
    public const int ResultMaxLength = 2000;
    public const int ErrorMaxLength = 200;

    protected BackgroundJob()
    {
    }

    public BackgroundJob(Guid organizationId, string kind, string payloadJson, DateTime nowUtc)
    {
        OrganizationId = DomainGuard.RequireId(organizationId, nameof(organizationId));
        Kind = DomainGuard.RequireText(kind, KindMaxLength, nameof(kind));
        PayloadJson = DomainGuard.RequireText(payloadJson, PayloadMaxLength, nameof(payloadJson));
        AvailableAtUtc = DomainGuard.ToUtc(nowUtc);
    }

    public Guid OrganizationId { get; private set; }

    public string Kind { get; private set; } = string.Empty;

    public string PayloadJson { get; private set; } = string.Empty;

    public BackgroundJobStatus Status { get; private set; } = BackgroundJobStatus.Queued;

    public DateTime AvailableAtUtc { get; private set; }

    public DateTime? LockedUntilUtc { get; private set; }

    public int Attempts { get; private set; }

    public string? ResultSummary { get; private set; }

    public string? LastError { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>Claims a due job, or a running job whose lease has expired. Returns false if the job is not claimable.</summary>
    public bool TryClaim(DateTime nowUtc, TimeSpan lease)
    {
        var due = Status == BackgroundJobStatus.Queued && AvailableAtUtc <= nowUtc;
        var abandoned = Status == BackgroundJobStatus.Running && LockedUntilUtc <= nowUtc;
        if (!due && !abandoned) return false;

        Status = BackgroundJobStatus.Running;
        LockedUntilUtc = nowUtc + lease;
        Attempts++;
        Touch();
        return true;
    }

    public void Succeed(string summary, DateTime nowUtc)
    {
        Status = BackgroundJobStatus.Succeeded;
        LockedUntilUtc = null;
        CompletedAtUtc = DomainGuard.ToUtc(nowUtc);
        ResultSummary = Truncate(DomainGuard.RequireText(summary, int.MaxValue, nameof(summary)), ResultMaxLength);
        LastError = null;
        Touch();
    }

    /// <summary>Records a failed attempt. The job returns to the queue until it reaches maxAttempts, then stays failed.</summary>
    public void Fail(string errorType, DateTime nowUtc, int maxAttempts, TimeSpan retryDelay)
    {
        LockedUntilUtc = null;
        LastError = Truncate(DomainGuard.RequireText(errorType, int.MaxValue, nameof(errorType)), ErrorMaxLength);
        if (Attempts >= maxAttempts)
        {
            Status = BackgroundJobStatus.Failed;
            CompletedAtUtc = DomainGuard.ToUtc(nowUtc);
        }
        else
        {
            Status = BackgroundJobStatus.Queued;
            AvailableAtUtc = DomainGuard.ToUtc(nowUtc) + retryDelay;
        }

        Touch();
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
