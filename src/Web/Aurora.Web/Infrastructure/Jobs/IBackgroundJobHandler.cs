namespace Aurora.Web.Infrastructure.Jobs;

/// <summary>
/// Runs one kind of background job. The handler runs in a scope whose tenant is the job's organization. It returns a
/// short human-readable summary, which is stored with the job. It must be idempotent, because a job can run twice.
/// </summary>
public interface IBackgroundJobHandler
{
    string Kind { get; }

    Task<string> HandleAsync(Guid organizationId, string payloadJson, CancellationToken ct);
}
