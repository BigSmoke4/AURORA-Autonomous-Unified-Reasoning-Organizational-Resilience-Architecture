using Aurora.Web.Hubs;
using Xunit;

namespace Aurora.Tests;

public class SimulationHubTests
{
    [Fact]
    public void GroupName_IsStableForTheSameOrganizationAndScenario()
    {
        var organizationId = Guid.NewGuid();
        var scenarioId = Guid.NewGuid();

        Assert.Equal(SimulationHub.GroupName(organizationId, scenarioId), SimulationHub.GroupName(organizationId, scenarioId));
    }

    [Fact]
    public void GroupName_DiffersAcrossOrganizations_SoTenantsCannotShareProgress()
    {
        var scenarioId = Guid.NewGuid();

        // The same scenario id under two tenants must map to two groups. The organization is part of the
        // group name, so a join from one tenant can never receive another tenant's progress.
        Assert.NotEqual(
            SimulationHub.GroupName(Guid.NewGuid(), scenarioId),
            SimulationHub.GroupName(Guid.NewGuid(), scenarioId));
    }

    [Fact]
    public void GroupName_DiffersAcrossScenariosWithinAnOrganization()
    {
        var organizationId = Guid.NewGuid();

        Assert.NotEqual(
            SimulationHub.GroupName(organizationId, Guid.NewGuid()),
            SimulationHub.GroupName(organizationId, Guid.NewGuid()));
    }
}
