using Aurora.Web.Models.Account;
using Aurora.Web.Models.Modules;
using Aurora.Web.Models.Strategy;
using Aurora.Web.Validation;
using Aurora.Web.Validation.Modules;
using Xunit;

namespace Aurora.Tests.Validation;

/// <summary>Checks the FluentValidation rules behind the form posts, including the cross-field rules.</summary>
public sealed class FormValidatorTests
{
    private const string ResidualMessage = "The residual value must be below the acquisition cost.";
    private const string ContractDateMessage = "The end date cannot precede the start date.";
    private const string ThresholdMessage = "Set at least one threshold.";
    private const string ThresholdOrderMessage = "The minimum threshold must be below the maximum threshold.";
    private const string DefectiveMessage = "Defective units cannot exceed actual units.";
    private const string DemandMessage = "Demand must be greater than zero.";

    [Fact]
    public async Task Login_RejectsMalformedEmailAndEmptyPassword()
    {
        var result = await new LoginViewModelValidator().ValidateAsync(
            new LoginViewModel { Email = "not-an-email", Password = string.Empty });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginViewModel.Email));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginViewModel.Password));
    }

    [Fact]
    public async Task Login_AcceptsWellFormedCredentials()
    {
        var result = await new LoginViewModelValidator().ValidateAsync(
            new LoginViewModel { Email = "analyst@example.com", Password = "Aurora-Test-Password-1!" });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task PhysicalAsset_RejectsResidualValueAtOrAboveCost()
    {
        var form = ValidAsset();
        form.ResidualValue = form.AcquisitionCost;

        var result = await new PhysicalAssetFormValidator().ValidateAsync(form);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PhysicalAssetForm.ResidualValue) && e.ErrorMessage == ResidualMessage);
    }

    [Fact]
    public async Task PhysicalAsset_AcceptsResidualValueBelowCost()
    {
        var result = await new PhysicalAssetFormValidator().ValidateAsync(ValidAsset());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task SupplierContract_RejectsEndBeforeStart()
    {
        var form = ValidContract();
        form.EndsOn = form.StartsOn.AddDays(-1);

        var result = await new SupplierContractFormValidator().ValidateAsync(form);

        Assert.Contains(result.Errors, e => e.ErrorMessage == ContractDateMessage);
    }

    [Fact]
    public async Task MetricDefinition_RequiresAThreshold()
    {
        var form = ValidMetric();
        form.MinimumThreshold = null;
        form.MaximumThreshold = null;

        var result = await new MetricDefinitionFormValidator().ValidateAsync(form);

        Assert.Equal(2, result.Errors.Count(e => e.ErrorMessage == ThresholdMessage));
    }

    [Fact]
    public async Task MetricDefinition_RejectsMinimumNotBelowMaximum()
    {
        var form = ValidMetric();
        form.MinimumThreshold = 10m;
        form.MaximumThreshold = 10m;

        var result = await new MetricDefinitionFormValidator().ValidateAsync(form);

        Assert.Contains(result.Errors, e => e.ErrorMessage == ThresholdOrderMessage);
    }

    [Fact]
    public async Task MetricDefinition_AcceptsOneSidedThreshold()
    {
        var form = ValidMetric();
        form.MaximumThreshold = null;

        var result = await new MetricDefinitionFormValidator().ValidateAsync(form);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ProductionRecord_RejectsMoreDefectsThanUnits()
    {
        var form = new ProductionRecordForm
        {
            FactoryId = Guid.NewGuid(),
            PeriodStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PlannedUnits = 100,
            ActualUnits = 90,
            DefectiveUnits = 91,
        };

        var result = await new ProductionRecordFormValidator().ValidateAsync(form);

        Assert.Contains(result.Errors, e => e.ErrorMessage == DefectiveMessage);
    }

    [Fact]
    public async Task Optimization_RejectsZeroDemand()
    {
        var form = new OptimizationForm { Demand = 0m, CostWeightPct = 50, CandidatesText = "A | 1 | 1 | 0.1 | 0.1" };

        var result = await new OptimizationFormValidator().ValidateAsync(form);

        Assert.Contains(result.Errors, e => e.ErrorMessage == DemandMessage);
    }

    [Fact]
    public async Task Counterfactual_RejectsMitigationShareOverOneHundred()
    {
        var form = new CounterfactualForm { ScenarioId = Guid.NewGuid(), MitigationSharePct = 101 };

        var result = await new CounterfactualFormValidator().ValidateAsync(form);

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CounterfactualForm.MitigationSharePct));
    }

    private static PhysicalAssetForm ValidAsset() => new()
    {
        Name = "CNC machining centre",
        Category = "Machinery",
        AcquisitionCost = 100000m,
        ResidualValue = 10000m,
        InServiceOn = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UsefulLifeYears = 10,
    };

    private static SupplierContractForm ValidContract() => new()
    {
        Reference = "NB-2026-01",
        Counterparty = "Northbridge Components",
        StartsOn = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        EndsOn = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        AnnualValue = 500000m,
        LatePenaltyPerDay = 100m,
        NoticePeriodDays = 30,
    };

    private static MetricDefinitionForm ValidMetric() => new()
    {
        Name = "Line 1 cycle time",
        Unit = "seconds",
        MinimumThreshold = 10m,
        MaximumThreshold = 60m,
    };
}
