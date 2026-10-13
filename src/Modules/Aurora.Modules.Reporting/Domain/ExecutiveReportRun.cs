using Aurora.Shared.Kernel;

namespace Aurora.Modules.Reporting.Domain;

/// <summary>
/// One audited export of the executive report. It records who exported, when, how many lines, and the SHA-256 of the
/// exact bytes delivered, so a file can later be matched to the run that produced it. It stores no report figures.
/// </summary>
public class ExecutiveReportRun : Entity, ITenantScoped
{
    public const int RequestedByNameMaxLength = 200;
    public const int ContentHashLength = 64;

    public Guid OrganizationId { get; private set; }
    public DateTime GeneratedAtUtc { get; private set; }
    public Guid? RequestedByUserId { get; private set; }
    public string RequestedByName { get; private set; } = string.Empty;
    public int LineCount { get; private set; }
    public string ContentSha256 { get; private set; } = string.Empty;

    protected ExecutiveReportRun()
    {
    }

    public ExecutiveReportRun(Guid organizationId, DateTime generatedAtUtc, Guid? requestedByUserId, string requestedByName,
        int lineCount, string contentSha256)
    {
        OrganizationId = DomainGuard.RequireId(organizationId, nameof(organizationId));
        GeneratedAtUtc = DomainGuard.ToUtc(generatedAtUtc);
        RequestedByUserId = requestedByUserId;
        RequestedByName = DomainGuard.RequireText(requestedByName, RequestedByNameMaxLength, nameof(requestedByName));
        LineCount = DomainGuard.RequireRange(lineCount, 0, int.MaxValue, nameof(lineCount));
        ContentSha256 = DomainGuard.RequireText(contentSha256, ContentHashLength, nameof(contentSha256));
    }
}
