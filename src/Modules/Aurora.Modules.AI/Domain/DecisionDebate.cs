using Aurora.Shared.Kernel;

namespace Aurora.Modules.AI.Domain;

public enum DebateRole
{
    Advocate = 1,
    Critic = 2,
    Judge = 3,
}

/// <summary>
/// One deterministic debate over a decision. The advocate and the critic each check the same recorded numbers against
/// fixed rules, and the judge applies a count-based verdict. No model produces any figure or verdict here.
/// </summary>
public class DebateSession : Entity, ITenantScoped
{
    public const int VerdictMaxLength = 60;
    public const int RationaleMaxLength = 1000;

    public Guid OrganizationId { get; private set; }
    public Guid DecisionId { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public int AdvocatePoints { get; private set; }
    public int CriticPoints { get; private set; }
    public string Verdict { get; private set; } = string.Empty;
    public string VerdictRationale { get; private set; } = string.Empty;

    protected DebateSession()
    {
    }

    public DebateSession(Guid organizationId, Guid decisionId, DateTime startedAtUtc, int advocatePoints, int criticPoints,
        string verdict, string verdictRationale)
    {
        OrganizationId = DomainGuard.RequireId(organizationId, nameof(organizationId));
        DecisionId = DomainGuard.RequireId(decisionId, nameof(decisionId));
        StartedAtUtc = DomainGuard.ToUtc(startedAtUtc);
        AdvocatePoints = DomainGuard.RequireRange(advocatePoints, 0, int.MaxValue, nameof(advocatePoints));
        CriticPoints = DomainGuard.RequireRange(criticPoints, 0, int.MaxValue, nameof(criticPoints));
        Verdict = DomainGuard.RequireText(verdict, VerdictMaxLength, nameof(verdict));
        VerdictRationale = DomainGuard.RequireText(verdictRationale, RationaleMaxLength, nameof(verdictRationale));
    }
}

/// <summary>A single argument in a debate, in the order it was made.</summary>
public class DebateArgument : Entity, ITenantScoped
{
    public const int ClaimMaxLength = 500;

    public Guid OrganizationId { get; private set; }
    public Guid DebateSessionId { get; private set; }
    public int Ordinal { get; private set; }
    public DebateRole Role { get; private set; }
    public bool Supports { get; private set; }
    public string Claim { get; private set; } = string.Empty;

    protected DebateArgument()
    {
    }

    public DebateArgument(Guid organizationId, Guid debateSessionId, int ordinal, DebateRole role, bool supports, string claim)
    {
        OrganizationId = DomainGuard.RequireId(organizationId, nameof(organizationId));
        DebateSessionId = DomainGuard.RequireId(debateSessionId, nameof(debateSessionId));
        Ordinal = DomainGuard.RequireRange(ordinal, 0, int.MaxValue, nameof(ordinal));
        Role = role;
        Supports = supports;
        Claim = DomainGuard.RequireText(claim, ClaimMaxLength, nameof(claim));
    }
}
