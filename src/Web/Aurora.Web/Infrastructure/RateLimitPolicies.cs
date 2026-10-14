using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Aurora.Web.Infrastructure;

/// <summary>Named rate-limit policies (spec section 44). Applied per endpoint with
/// <c>[EnableRateLimiting]</c>. Limits are fixed-window and partitioned per client.</summary>
public static class RateLimitPolicies
{
    public const string Authentication = "authentication";
    public const string AiInvestigation = "ai-investigation";
    public const string Analysis = "analysis";

    public static void Configure(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddPolicy(Authentication, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

        options.AddPolicy(AiInvestigation, context => RateLimitPartition.GetFixedWindowLimiter(
            PartitionKey(context),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

        options.AddPolicy(Analysis, context => RateLimitPartition.GetFixedWindowLimiter(
            PartitionKey(context),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    }

    private static string PartitionKey(HttpContext context) =>
        context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
