using Aurora.Shared.Kernel;

namespace Aurora.Modules.Customers.Application;

public enum ConcentrationBand
{
    Unconcentrated = 1,
    ModeratelyConcentrated = 2,
    HighlyConcentrated = 3,
}

public sealed record CustomerExposure(string Name, decimal Exposure);

public sealed record CustomerShare(string Name, decimal Exposure, decimal SharePct);

/// <summary>A customer with its identity, for the per-customer impact view.</summary>
public sealed record KeyedCustomerExposure(Guid CustomerId, string Name, decimal Exposure);

/// <summary>
/// What one customer means for concentration: its share, its rank by exposure, and the index and band of the book
/// if that customer were removed. Removal is a what-if on the current book, not a forecast of churn.
/// </summary>
public sealed record CustomerImpact(
    Guid CustomerId,
    string Name,
    decimal Exposure,
    decimal SharePct,
    int Rank,
    int CustomerCount,
    decimal Hhi,
    decimal HhiWithoutCustomer,
    ConcentrationBand BandWithoutCustomer);

public sealed record ConcentrationResult(
    decimal TotalExposure,
    decimal TopCustomerSharePct,
    decimal TopThreeSharePct,
    decimal Hhi,
    ConcentrationBand Band,
    IReadOnlyList<CustomerShare> Customers);

/// <summary>
/// Revenue-exposure concentration (spec section 29). The Herfindahl-Hirschman index is the sum of squared
/// percentage shares, on a 0 to 10,000 scale. The bands follow the conventional US antitrust thresholds
/// (1,500 and 2,500) and are reported as a descriptive signal, not a legal finding.
/// </summary>
public static class CustomerConcentrationCalculator
{
    public const decimal ModeratelyConcentratedHhi = 1500m;
    public const decimal HighlyConcentratedHhi = 2500m;
    public const int TopCustomerCount = 3;

    public static ConcentrationResult Analyze(IReadOnlyList<CustomerExposure> customers)
    {
        ArgumentNullException.ThrowIfNull(customers);
        if (customers.Any(c => c.Exposure < 0m)) throw new ArgumentOutOfRangeException(nameof(customers), "Exposure must not be negative.");

        var total = customers.Sum(c => c.Exposure);
        var ordered = customers.OrderByDescending(c => c.Exposure).ThenBy(c => c.Name, StringComparer.Ordinal).ToList();

        if (total == 0m)
        {
            var empty = ordered.Select(c => new CustomerShare(c.Name, c.Exposure, 0m)).ToList();
            return new ConcentrationResult(0m, 0m, 0m, 0m, ConcentrationBand.Unconcentrated, empty);
        }

        var exactShares = ordered.Select(c => c.Exposure * 100m / total).ToList();
        var hhi = Rounding.Round(exactShares.Sum(s => s * s));
        var top = exactShares.Count > 0 ? Rounding.Round(exactShares[0]) : 0m;
        var topThree = Rounding.Round(exactShares.Take(TopCustomerCount).Sum());

        var band = hhi >= HighlyConcentratedHhi ? ConcentrationBand.HighlyConcentrated
            : hhi >= ModeratelyConcentratedHhi ? ConcentrationBand.ModeratelyConcentrated
            : ConcentrationBand.Unconcentrated;

        var shares = ordered.Select((c, i) => new CustomerShare(c.Name, c.Exposure, Rounding.Round(exactShares[i]))).ToList();

        return new ConcentrationResult(Rounding.Round(total), top, topThree, hhi, band, shares);
    }

    /// <summary>Impact of one customer on the book. Returns null when the customer is not in the book.</summary>
    public static CustomerImpact? Impact(IReadOnlyList<KeyedCustomerExposure> customers, Guid customerId)
    {
        ArgumentNullException.ThrowIfNull(customers);
        var target = customers.FirstOrDefault(c => c.CustomerId == customerId);
        if (target is null) return null;

        var total = customers.Sum(c => c.Exposure);
        var share = total == 0m ? 0m : Rounding.Round(target.Exposure * 100m / total);
        var rank = 1 + customers.Count(c => c.CustomerId != customerId && c.Exposure > target.Exposure);
        var full = Analyze(customers.Select(c => new CustomerExposure(c.Name, c.Exposure)).ToList());
        var without = Analyze(customers
            .Where(c => c.CustomerId != customerId)
            .Select(c => new CustomerExposure(c.Name, c.Exposure))
            .ToList());

        return new CustomerImpact(target.CustomerId, target.Name, target.Exposure, share, rank, customers.Count,
            full.Hhi, without.Hhi, without.Band);
    }
}
