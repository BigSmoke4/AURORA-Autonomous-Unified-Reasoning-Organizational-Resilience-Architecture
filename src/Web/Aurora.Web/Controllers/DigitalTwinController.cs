using Aurora.Modules.DigitalTwin.Contracts;
using Aurora.Web.Models;
using Aurora.Web.Models.DigitalTwin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Web.Controllers;

[Authorize]
public class DigitalTwinController : Controller
{
    private readonly CurrentOrganizationAccessor _orgAccessor;
    private readonly IDigitalTwinReadService _digitalTwin;

    public DigitalTwinController(CurrentOrganizationAccessor orgAccessor, IDigitalTwinReadService digitalTwin)
    {
        _orgAccessor = orgAccessor;
        _digitalTwin = digitalTwin;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var org = await _orgAccessor.GetCurrentAsync(ct);
        if (org is null) return View("NoOrganization");

        var vm = new DigitalTwinViewModel
        {
            Suppliers = await _digitalTwin.GetSuppliersAsync(org.Id, ct),
            Factories = await _digitalTwin.GetFactoriesAsync(org.Id, ct),
            Warehouses = await _digitalTwin.GetWarehousesAsync(org.Id, ct),
            Products = await _digitalTwin.GetProductsAsync(org.Id, ct),
            Customers = await _digitalTwin.GetCustomersAsync(org.Id, ct),
            TechnologyAssets = await _digitalTwin.GetTechnologyAssetsAsync(org.Id, ct)
        };
        return View(vm);
    }
}
