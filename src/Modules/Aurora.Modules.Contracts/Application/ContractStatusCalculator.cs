using Aurora.Shared.Kernel;

namespace Aurora.Modules.Contracts.Application;

public enum ContractState
{
    NotStarted = 1,
    Active = 2,
    RenewalNoticeDue = 3,
    Expired = 4,
}

public sealed record ContractStatusResult(ContractState State, int DaysRemaining);

/// <summary>
/// Deterministic contract state (spec section 25). Days remaining are whole UTC calendar days to the end date.
/// The renewal notice window opens when the remaining days fall to the notice period or below.
/// </summary>
public static class ContractStatusCalculator
{
    public static ContractStatusResult Evaluate(DateTime startsOnUtc, DateTime endsOnUtc, int noticePeriodDays, DateTime asOfUtc)
    {
        if (noticePeriodDays < 0) throw new ArgumentOutOfRangeException(nameof(noticePeriodDays), noticePeriodDays, "The notice period must not be negative.");

        var asOfDate = asOfUtc.Date;
        var daysRemaining = (endsOnUtc.Date - asOfDate).Days;

        var state = asOfDate < startsOnUtc.Date ? ContractState.NotStarted
            : daysRemaining < 0 ? ContractState.Expired
            : daysRemaining <= noticePeriodDays ? ContractState.RenewalNoticeDue
            : ContractState.Active;

        return new ContractStatusResult(state, daysRemaining);
    }

    /// <summary>Penalty exposure for a delivery that runs late by the given number of days.</summary>
    public static decimal DelayPenaltyExposure(decimal latePenaltyPerDay, int delayDays)
    {
        if (latePenaltyPerDay < 0m) throw new ArgumentOutOfRangeException(nameof(latePenaltyPerDay), latePenaltyPerDay, "The penalty must not be negative.");
        if (delayDays < 0) throw new ArgumentOutOfRangeException(nameof(delayDays), delayDays, "The delay must not be negative.");

        return Rounding.Round(latePenaltyPerDay * delayDays);
    }
}
