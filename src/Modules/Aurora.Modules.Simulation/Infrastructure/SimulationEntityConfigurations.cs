using Aurora.Modules.Simulation.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.Simulation.Infrastructure;

public class SimulationRunConfig : IEntityTypeConfiguration<SimulationRun>
{
    public void Configure(EntityTypeBuilder<SimulationRun> b)
    {
        b.ToTable("SimulationRuns");
        b.HasKey(x => x.Id);
        b.Property(x => x.ModelVersion).HasMaxLength(60);
        b.Property(x => x.ErrorMessage).HasMaxLength(2000);
        b.HasIndex(x => x.OrganizationId);
        b.HasIndex(x => x.ScenarioId);
    }
}

public class SimulationStepConfig : IEntityTypeConfiguration<SimulationStep>
{
    public void Configure(EntityTypeBuilder<SimulationStep> b)
    {
        b.ToTable("SimulationSteps");
        b.HasKey(x => x.Id);
        b.Property(x => x.ImpactScore).HasColumnType("decimal(6,4)");
        b.Property(x => x.RevenueImpact).HasColumnType("decimal(18,2)");
        b.Property(x => x.Status).HasMaxLength(30);
        b.HasIndex(x => new { x.SimulationRunId, x.DayNumber });
    }
}

public class SimulationResultConfig : IEntityTypeConfiguration<SimulationResult>
{
    public void Configure(EntityTypeBuilder<SimulationResult> b)
    {
        b.ToTable("SimulationResults");
        b.HasKey(x => x.Id);
        b.Property(x => x.TotalRevenueImpact).HasColumnType("decimal(18,2)");
        b.Property(x => x.PeakDailyImpactScore).HasColumnType("decimal(6,4)");
        b.Property(x => x.Summary).HasMaxLength(1000);
        b.HasIndex(x => x.SimulationRunId).IsUnique();
    }
}
