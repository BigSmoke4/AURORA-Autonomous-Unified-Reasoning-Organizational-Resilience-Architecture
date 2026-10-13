using Aurora.Modules.Assets.Domain;
using Aurora.Modules.Contracts.Domain;
using Aurora.Modules.Finance.Domain;
using Aurora.Modules.Markets.Domain;
using Aurora.Modules.Monitoring.Domain;
using Aurora.Modules.Operations.Domain;
using Aurora.Modules.SupplyChain.Domain;
using Aurora.Modules.Technology.Domain;
using Aurora.Modules.Workforce.Domain;
using Aurora.Web.Models.Modules;
using FluentValidation;

namespace Aurora.Web.Validation.Modules;

public sealed class WorkforceRoleFormValidator : AbstractValidator<WorkforceRoleForm>
{
    public WorkforceRoleFormValidator()
    {
        RuleFor(f => f.Name).NotEmpty().MaximumLength(CriticalRole.NameMaxLength);
        RuleFor(f => f.RoleTitle).NotEmpty().MaximumLength(CriticalRole.NameMaxLength);
        RuleFor(f => f.RequiredHeadcount).InclusiveBetween(CriticalRole.MinHeadcount, CriticalRole.MaxHeadcount);
        RuleFor(f => f.Criticality).InclusiveBetween(CriticalRole.MinCriticality, CriticalRole.MaxCriticality);
    }
}

public sealed class CostCenterFormValidator : AbstractValidator<CostCenterForm>
{
    public CostCenterFormValidator()
    {
        RuleFor(f => f.Code).NotEmpty().MaximumLength(CostCenter.CodeMaxLength);
        RuleFor(f => f.Name).NotEmpty().MaximumLength(CostCenter.NameMaxLength);
        RuleFor(f => f.AnnualBudget).InclusiveBetween(ModuleFormatting.MinimumMoney, ModuleFormatting.MaximumMoney);
        RuleFor(f => f.ActualSpend).InclusiveBetween(ModuleFormatting.MinimumMoney, ModuleFormatting.MaximumMoney);
    }
}

public sealed class ProductionRecordFormValidator : AbstractValidator<ProductionRecordForm>
{
    private const int MinimumUnits = 0;
    private const int MinimumPlannedUnits = 1;

    public ProductionRecordFormValidator()
    {
        RuleFor(f => f.FactoryId).NotEmpty();
        RuleFor(f => f.PeriodStart).NotEmpty();
        RuleFor(f => f.PlannedUnits).GreaterThanOrEqualTo(MinimumPlannedUnits);
        RuleFor(f => f.ActualUnits).GreaterThanOrEqualTo(MinimumUnits);
        RuleFor(f => f.DefectiveUnits).GreaterThanOrEqualTo(MinimumUnits);
        RuleFor(f => f.DefectiveUnits)
            .LessThanOrEqualTo(f => f.ActualUnits)
            .WithMessage("Defective units cannot exceed actual units.");
    }
}

public sealed class PatchPolicyFormValidator : AbstractValidator<PatchPolicyForm>
{
    public PatchPolicyFormValidator()
    {
        RuleFor(f => f.TechnologyAssetId).NotEmpty();
        RuleFor(f => f.LastPatchedOn).NotEmpty();
        RuleFor(f => f.PatchSlaDays).InclusiveBetween(PatchPolicy.MinSlaDays, PatchPolicy.MaxSlaDays);
    }
}

public sealed class RecordPatchFormValidator : AbstractValidator<RecordPatchForm>
{
    public RecordPatchFormValidator()
    {
        RuleFor(f => f.PatchPolicyId).NotEmpty();
        RuleFor(f => f.PatchedOn).NotEmpty();
    }
}

public sealed class MarketObservationFormValidator : AbstractValidator<MarketObservationForm>
{
    public MarketObservationFormValidator()
    {
        RuleFor(f => f.Market).NotEmpty().MaximumLength(MarketObservation.MarketMaxLength);
        RuleFor(f => f.ObservedOn).NotEmpty();
        RuleFor(f => f.DemandIndex).InclusiveBetween(ModuleFormatting.MinimumIndex, ModuleFormatting.MaximumIndex);
        RuleFor(f => f.PriceIndex).InclusiveBetween(ModuleFormatting.MinimumIndex, ModuleFormatting.MaximumIndex);
    }
}

public sealed class PhysicalAssetFormValidator : AbstractValidator<PhysicalAssetForm>
{
    public PhysicalAssetFormValidator()
    {
        RuleFor(f => f.Name).NotEmpty().MaximumLength(PhysicalAsset.NameMaxLength);
        RuleFor(f => f.Category).NotEmpty().MaximumLength(PhysicalAsset.CategoryMaxLength);
        RuleFor(f => f.AcquisitionCost)
            .InclusiveBetween(ModuleFormatting.MinimumPositiveMoney, ModuleFormatting.MaximumMoney);
        RuleFor(f => f.ResidualValue)
            .InclusiveBetween(ModuleFormatting.MinimumMoney, ModuleFormatting.MaximumMoney);
        RuleFor(f => f.ResidualValue)
            .LessThan(f => f.AcquisitionCost)
            .WithMessage("The residual value must be below the acquisition cost.");
        RuleFor(f => f.InServiceOn).NotEmpty();
        RuleFor(f => f.UsefulLifeYears)
            .InclusiveBetween(PhysicalAsset.MinUsefulLifeYears, PhysicalAsset.MaxUsefulLifeYears);
    }
}

public sealed class SupplierContractFormValidator : AbstractValidator<SupplierContractForm>
{
    public SupplierContractFormValidator()
    {
        RuleFor(f => f.Reference).NotEmpty().MaximumLength(SupplierContract.ReferenceMaxLength);
        RuleFor(f => f.Counterparty).NotEmpty().MaximumLength(SupplierContract.CounterpartyMaxLength);
        RuleFor(f => f.StartsOn).NotEmpty();
        RuleFor(f => f.EndsOn).NotEmpty();
        RuleFor(f => f.EndsOn)
            .GreaterThanOrEqualTo(f => f.StartsOn)
            .WithMessage("The end date cannot precede the start date.");
        RuleFor(f => f.AnnualValue).InclusiveBetween(ModuleFormatting.MinimumMoney, ModuleFormatting.MaximumMoney);
        RuleFor(f => f.LatePenaltyPerDay).InclusiveBetween(ModuleFormatting.MinimumMoney, ModuleFormatting.MaximumMoney);
        RuleFor(f => f.NoticePeriodDays).InclusiveBetween(0, SupplierContract.MaxNoticePeriodDays);
    }
}

public sealed class MetricDefinitionFormValidator : AbstractValidator<MetricDefinitionForm>
{
    private const string ThresholdRequiredMessage = "Set at least one threshold.";
    private const string ThresholdOrderMessage = "The minimum threshold must be below the maximum threshold.";
    private const string ThresholdRangeMessage = "The threshold is outside the allowed range.";

    public MetricDefinitionFormValidator()
    {
        RuleFor(f => f.Name).NotEmpty().MaximumLength(MetricDefinition.NameMaxLength);
        RuleFor(f => f.Unit).NotEmpty().MaximumLength(MetricDefinition.UnitMaxLength);
        RuleFor(f => f.MinimumThreshold)
            .Must(v => v is null || (v >= ModuleFormatting.MinimumReadingValue && v <= ModuleFormatting.MaximumReadingValue))
            .WithMessage(ThresholdRangeMessage);
        RuleFor(f => f.MaximumThreshold)
            .Must(v => v is null || (v >= ModuleFormatting.MinimumReadingValue && v <= ModuleFormatting.MaximumReadingValue))
            .WithMessage(ThresholdRangeMessage);
        RuleFor(f => f.MinimumThreshold)
            .NotNull()
            .When(f => f.MaximumThreshold is null)
            .WithMessage(ThresholdRequiredMessage);
        RuleFor(f => f.MaximumThreshold)
            .NotNull()
            .When(f => f.MinimumThreshold is null)
            .WithMessage(ThresholdRequiredMessage);
        RuleFor(f => f.MinimumThreshold)
            .Must((f, low) => low is null || f.MaximumThreshold is null || low < f.MaximumThreshold)
            .WithMessage(ThresholdOrderMessage);
    }
}

public sealed class MetricReadingFormValidator : AbstractValidator<MetricReadingForm>
{
    public MetricReadingFormValidator()
    {
        RuleFor(f => f.MetricDefinitionId).NotEmpty();
        RuleFor(f => f.Value).InclusiveBetween(ModuleFormatting.MinimumReadingValue, ModuleFormatting.MaximumReadingValue);
        RuleFor(f => f.RecordedOn).NotEmpty();
    }
}

public sealed class InventoryPositionFormValidator : AbstractValidator<InventoryPositionForm>
{
    private const int MinimumCount = 0;

    public InventoryPositionFormValidator()
    {
        RuleFor(f => f.Sku).NotEmpty().MaximumLength(InventoryPosition.SkuMaxLength);
        RuleFor(f => f.Location).NotEmpty().MaximumLength(InventoryPosition.LocationMaxLength);
        RuleFor(f => f.OnHandUnits).GreaterThanOrEqualTo(MinimumCount);
        RuleFor(f => f.AverageDailyDemand).InclusiveBetween(ModuleFormatting.MinimumMoney, ModuleFormatting.MaximumDemand);
        RuleFor(f => f.ReplenishmentLeadTimeDays).InclusiveBetween(MinimumCount, InventoryPosition.MaxLeadTimeDays);
        RuleFor(f => f.SafetyStockUnits).GreaterThanOrEqualTo(MinimumCount);
    }
}
