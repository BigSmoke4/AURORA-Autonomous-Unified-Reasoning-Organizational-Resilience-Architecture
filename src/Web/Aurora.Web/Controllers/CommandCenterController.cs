using Aurora.Modules.DigitalTwin.Contracts;
using Aurora.Modules.Graph.Application;
using Aurora.Web.Models;
using Aurora.Web.Models.CommandCenter;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Web.Controllers;

[Authorize]
public class CommandCenterController : Controller
{
    private readonly CurrentOrganizationAccessor _orgAccessor;
    private readonly IGraphProjectionService _graphProjection;
    private readonly GraphTraversalService _traversal;
    private readonly IDigitalTwinReadService _digitalTwin;

    public CommandCenterController(
        CurrentOrganizationAccessor orgAccessor,
        IGraphProjectionService graphProjection,
        GraphTraversalService traversal,
        IDigitalTwinReadService digitalTwin)
    {
        _orgAccessor = orgAccessor;
        _graphProjection = graphProjection;
        _traversal = traversal;
        _digitalTwin = digitalTwin;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return View("NoOrganization");

        var snapshot = await _graphProjection.BuildLiveGraphAsync(org.Id, ct);
        var spofs = _traversal.DetectSinglePointsOfFailure(snapshot);

        var suppliers = await _digitalTwin.GetSuppliersAsync(org.Id, ct);
        var factories = await _digitalTwin.GetFactoriesAsync(org.Id, ct);
        var products = await _digitalTwin.GetProductsAsync(org.Id, ct);
        var customers = await _digitalTwin.GetCustomersAsync(org.Id, ct);

        var vm = new CommandCenterViewModel
        {
            OrganizationName = org.Name,
            NodeCount = snapshot.Nodes.Count,
            EdgeCount = snapshot.Edges.Count,
            SupplierCount = suppliers.Count,
            FactoryCount = factories.Count,
            ProductCount = products.Count,
            CustomerCount = customers.Count,
            TotalRevenueExposure = snapshot.Nodes.Values.Sum(n => n.RevenueExposure),
            CriticalNodeCount = snapshot.Nodes.Values.Count(n => n.Status == "Critical"),
            WarningNodeCount = snapshot.Nodes.Values.Count(n => n.Status == "Warning"),
            SinglePointsOfFailureCount = spofs.Count,
            ActiveScenarioCount = 0,
            OpenDecisionCount = 0
        };

        ViewBag.OrganizationId = org.Id;
        return View(vm);
    }
}
