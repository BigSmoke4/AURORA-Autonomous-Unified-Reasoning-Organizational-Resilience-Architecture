using Aurora.Modules.DigitalTwin.Domain;

namespace Aurora.Web.Models.DigitalTwin;

public class DigitalTwinViewModel
{
    public IReadOnlyList<Supplier> Suppliers { get; set; } = new List<Supplier>();
    public IReadOnlyList<Factory> Factories { get; set; } = new List<Factory>();
    public IReadOnlyList<Warehouse> Warehouses { get; set; } = new List<Warehouse>();
    public IReadOnlyList<Product> Products { get; set; } = new List<Product>();
    public IReadOnlyList<Customer> Customers { get; set; } = new List<Customer>();
    public IReadOnlyList<TechnologyAsset> TechnologyAssets { get; set; } = new List<TechnologyAsset>();
}
