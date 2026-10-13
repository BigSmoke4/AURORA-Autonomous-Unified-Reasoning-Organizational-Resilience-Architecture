using Aurora.Shared.Kernel;

namespace Aurora.Modules.Contracts.Domain;

/// <summary>
/// A supply or service contract with its term, renewal notice window and late-delivery penalty (spec section 25).
/// </summary>
public class SupplierContract : Entity, ITenantScoped
{
    public const int ReferenceMaxLength = 60;
    public const int CounterpartyMaxLength = 200;
    public const int MaxNoticePeriodDays = 365;

    public Guid OrganizationId { get; private set; }
    public string Reference { get; private set; } = string.Empty;
    public string Counterparty { get; private set; } = string.Empty;
    public DateTime StartsOnUtc { get; private set; }
    public DateTime EndsOnUtc { get; private set; }
    public decimal AnnualValue { get; private set; }
    public decimal LatePenaltyPerDay { get; private set; }
    public int NoticePeriodDays { get; private set; }

    protected SupplierContract()
    {
    }

    public SupplierContract(Guid organizationId, string reference, string counterparty, DateTime startsOnUtc, DateTime endsOnUtc, decimal annualValue, decimal latePenaltyPerDay, int noticePeriodDays)
    {
        OrganizationId = DomainGuard.RequireId(organizationId, nameof(organizationId));
        Reference = DomainGuard.RequireText(reference, ReferenceMaxLength, nameof(reference));
        Counterparty = DomainGuard.RequireText(counterparty, CounterpartyMaxLength, nameof(counterparty));
        StartsOnUtc = DomainGuard.ToUtc(startsOnUtc);
        EndsOnUtc = DomainGuard.ToUtc(endsOnUtc);
        if (EndsOnUtc < StartsOnUtc) throw new ArgumentException("The end date cannot precede the start date.", nameof(endsOnUtc));
        AnnualValue = DomainGuard.RequireNonNegative(annualValue, nameof(annualValue));
        LatePenaltyPerDay = DomainGuard.RequireNonNegative(latePenaltyPerDay, nameof(latePenaltyPerDay));
        NoticePeriodDays = DomainGuard.RequireRange(noticePeriodDays, 0, MaxNoticePeriodDays, nameof(noticePeriodDays));
    }
}
