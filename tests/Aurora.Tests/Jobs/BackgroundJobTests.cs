using Aurora.Web.Data.Jobs;
using Xunit;

namespace Aurora.Tests.Jobs;

public sealed class BackgroundJobTests
{
    private static readonly DateTime Now = new(2026, 10, 13, 9, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    private static BackgroundJob NewJob() => new(Guid.NewGuid(), "feeds.metrics.ingest", "{}", Now);

    [Fact]
    public void DueJobIsClaimedOnceAndRecordsTheAttempt()
    {
        var job = NewJob();

        Assert.True(job.TryClaim(Now, Lease));
        Assert.False(job.TryClaim(Now, Lease));
        Assert.Equal(BackgroundJobStatus.Running, job.Status);
        Assert.Equal(1, job.Attempts);
        Assert.Equal(Now + Lease, job.LockedUntilUtc);
    }

    [Fact]
    public void JobIsNotClaimableBeforeItsAvailableTime()
    {
        var job = NewJob();

        Assert.False(job.TryClaim(Now.AddSeconds(-1), Lease));
        Assert.Equal(BackgroundJobStatus.Queued, job.Status);
    }

    [Fact]
    public void RunningJobWhoseLeaseExpiredIsClaimedAgain()
    {
        var job = NewJob();
        job.TryClaim(Now, Lease);

        Assert.True(job.TryClaim(Now + Lease, Lease));
        Assert.Equal(2, job.Attempts);
    }

    [Fact]
    public void SuccessRecordsTheSummaryAndClearsTheLease()
    {
        var job = NewJob();
        job.TryClaim(Now, Lease);

        job.Succeed("3 readings recorded.", Now.AddSeconds(2));

        Assert.Equal(BackgroundJobStatus.Succeeded, job.Status);
        Assert.Null(job.LockedUntilUtc);
        Assert.Equal("3 readings recorded.", job.ResultSummary);
        Assert.Equal(Now.AddSeconds(2), job.CompletedAtUtc);
    }

    [Fact]
    public void FailureBeforeTheLimitReturnsTheJobToTheQueueAfterTheDelay()
    {
        var job = NewJob();
        job.TryClaim(Now, Lease);

        job.Fail("TimeoutException", Now.AddSeconds(1), maxAttempts: 3, RetryDelay);

        Assert.Equal(BackgroundJobStatus.Queued, job.Status);
        Assert.Equal(Now.AddSeconds(1) + RetryDelay, job.AvailableAtUtc);
        Assert.Equal("TimeoutException", job.LastError);
        Assert.Null(job.CompletedAtUtc);
    }

    [Fact]
    public void FailureAtTheLimitMarksTheJobFailed()
    {
        var job = NewJob();
        const int maxAttempts = 2;
        job.TryClaim(Now, Lease);
        job.Fail("First", Now, maxAttempts, RetryDelay);
        job.TryClaim(Now.AddSeconds(60), Lease);

        job.Fail("Second", Now.AddSeconds(61), maxAttempts, RetryDelay);

        Assert.Equal(BackgroundJobStatus.Failed, job.Status);
        Assert.Equal("Second", job.LastError);
        Assert.Equal(Now.AddSeconds(61), job.CompletedAtUtc);
    }

    [Fact]
    public void FailedJobIsNotClaimable()
    {
        var job = NewJob();
        job.TryClaim(Now, Lease);
        job.Fail("Boom", Now, maxAttempts: 1, RetryDelay);

        Assert.False(job.TryClaim(Now.AddDays(1), Lease));
    }
}
