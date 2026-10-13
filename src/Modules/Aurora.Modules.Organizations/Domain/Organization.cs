using Aurora.Shared.Kernel;

namespace Aurora.Modules.Organizations.Domain;

/// <summary>
/// The tenant root. Every organization-scoped entity across every module carries an
/// OrganizationId that must resolve to a row here (spec section 42, multi-tenancy).
/// </summary>
public class Organization : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string Industry { get; private set; } = string.Empty;
    public string? HeadquartersLocation { get; private set; }
    public bool IsActive { get; private set; } = true;

    private readonly List<BusinessUnit> _businessUnits = new();
    public IReadOnlyCollection<BusinessUnit> BusinessUnits => _businessUnits.AsReadOnly();

    protected Organization() { }

    public Organization(string name, string industry, string? headquartersLocation = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Organization name is required.", nameof(name));
        Name = name;
        Industry = industry;
        HeadquartersLocation = headquartersLocation;
    }
}
