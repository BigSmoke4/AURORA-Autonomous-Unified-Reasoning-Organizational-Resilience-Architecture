using Aurora.Shared.Kernel;

namespace Aurora.Modules.Markets.Domain;

/// <summary>
/// One demand and price index reading for a market. Indices are user-supplied on any consistent base,
/// so only relative change is meaningful (spec section 23).
/// </summary>
public class MarketObservation : Entity, ITenantScoped
{
    public const int MarketMaxLength = 200;

    public Guid OrganizationId { get; private set; }
    public string Market { get; private set; } = string.Empty;
    public DateTime ObservedOnUtc { get; private set; }
    public decimal DemandIndex { get; private set; }
    public decimal PriceIndex { get; private set; }

    protected MarketObservation()
    {
    }

    public MarketObservation(Guid organizationId, string market, DateTime observedOnUtc, decimal demandIndex, decimal priceIndex)
    {
        OrganizationId = DomainGuard.RequireId(organizationId, nameof(organizationId));
        Market = DomainGuard.RequireText(market, MarketMaxLength, nameof(market));
        ObservedOnUtc = DomainGuard.ToUtc(observedOnUtc);
        DemandIndex = DomainGuard.RequirePositive(demandIndex, nameof(demandIndex));
        PriceIndex = DomainGuard.RequirePositive(priceIndex, nameof(priceIndex));
    }
}
