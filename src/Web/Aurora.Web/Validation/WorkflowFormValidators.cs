using Aurora.Modules.Forecasting.Application;
using Aurora.Web.Models.Account;
using Aurora.Web.Models.Analysis;
using Aurora.Web.Models.Decisions;
using Aurora.Web.Models.Forecasting;
using Aurora.Web.Models.Modules;
using Aurora.Web.Models.Strategy;
using FluentValidation;

namespace Aurora.Web.Validation;

public sealed class ProposeDecisionFormValidator : AbstractValidator<ProposeDecisionForm>
{
    private const int MaximumDaysToBenefit = 365;

    public ProposeDecisionFormValidator()
    {
        RuleFor(f => f.AnalysisRecordId).NotEmpty();
        RuleFor(f => f.OperationalFeasibility)
            .InclusiveBetween(ValidationLimits.MinimumPercent, ValidationLimits.MaximumPercent);
        RuleFor(f => f.StrategicAlignment)
            .InclusiveBetween(ValidationLimits.MinimumPercent, ValidationLimits.MaximumPercent);
        RuleFor(f => f.DaysToBenefit).InclusiveBetween(0, MaximumDaysToBenefit);
        RuleFor(f => f.Confidence)
            .InclusiveBetween(ValidationLimits.MinimumPercent, ValidationLimits.MaximumPercent);
    }
}

public sealed class RecordOutcomeFormValidator : AbstractValidator<RecordOutcomeForm>
{
    private const decimal MaximumRevenueImpact = 1000000000000m;
    private const int MinimumSummaryLength = 3;

    public RecordOutcomeFormValidator()
    {
        RuleFor(f => f.ActualRevenueImpact)
            .InclusiveBetween(-MaximumRevenueImpact, MaximumRevenueImpact)
            .WithMessage("Enter a realistic revenue impact.");
        RuleFor(f => f.ActualOutcomeSummary)
            .NotEmpty()
            .Length(MinimumSummaryLength, RecordOutcomeForm.MaxSummaryLength);
    }
}

public sealed class ForecastFormValidator : AbstractValidator<ForecastForm>
{
    public ForecastFormValidator()
    {
        RuleFor(f => f.SeriesText).NotEmpty().MaximumLength(ForecastForm.MaxSeriesTextLength);
        RuleFor(f => f.Horizon).InclusiveBetween(1, HoltForecaster.MaximumHorizon);
    }
}

public sealed class CounterfactualFormValidator : AbstractValidator<CounterfactualForm>
{
    public CounterfactualFormValidator()
    {
        RuleFor(f => f.ScenarioId).NotEmpty();
        RuleFor(f => f.MitigationSharePct)
            .InclusiveBetween(ValidationLimits.MinimumPercentPoints, ValidationLimits.MaximumPercentPoints);
    }
}

public sealed class OptimizationFormValidator : AbstractValidator<OptimizationForm>
{
    private const decimal MinimumDemand = 0.0001m;

    public OptimizationFormValidator()
    {
        RuleFor(f => f.Demand)
            .InclusiveBetween(MinimumDemand, ModuleFormatting.MaximumDemand)
            .WithMessage("Demand must be greater than zero.");
        RuleFor(f => f.CostWeightPct)
            .InclusiveBetween(ValidationLimits.MinimumPercentPoints, ValidationLimits.MaximumPercentPoints);
        RuleFor(f => f.CandidatesText).NotEmpty().MaximumLength(OptimizationForm.MaxCandidateTextLength);
    }
}

public sealed class LoginViewModelValidator : AbstractValidator<LoginViewModel>
{
    public LoginViewModelValidator()
    {
        RuleFor(m => m.Email).NotEmpty().EmailAddress().MaximumLength(ValidationLimits.MaximumEmailLength);
        RuleFor(m => m.Password).NotEmpty();
    }
}
