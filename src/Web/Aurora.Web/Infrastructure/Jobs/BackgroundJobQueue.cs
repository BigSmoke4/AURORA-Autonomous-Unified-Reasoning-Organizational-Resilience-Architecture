using Aurora.Web.Data;
using Aurora.Web.Data.Jobs;

namespace Aurora.Web.Infrastructure.Jobs;

/// <summary>Adds jobs to the queue. Stages the job only; the caller commits with SaveChangesAsync.</summary>
public sealed class BackgroundJobQueue(AuroraDbContext db, TimeProvider clock)
{
    public BackgroundJob Enqueue(Guid organizationId, string kind, string payloadJson)
    {
        var job = new BackgroundJob(organizationId, kind, payloadJson, clock.GetUtcNow().UtcDateTime);
        db.BackgroundJobs.Add(job);
        return job;
    }
}
