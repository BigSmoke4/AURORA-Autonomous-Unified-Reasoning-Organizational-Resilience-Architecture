namespace Aurora.Web.Models.Modules;

/// <summary>One background job as shown in the job list. Summary and error text are kept short by the job entity.</summary>
public sealed record BackgroundJobRow(Guid Id, string Kind, string Status, int Attempts, string? Summary, string? LastError,
    DateTime? CompletedAtUtc);
