namespace Aurora.Modules.Graph.Domain;

/// <summary>Spec section 5 entity catalog, the subset actually represented in the graph.</summary>
public enum GraphNodeType
{
    Organization = 0,
    BusinessUnit = 1,
    Department = 2,
    Employee = 3,
    Supplier = 4,
    Factory = 5,
    Warehouse = 6,
    Product = 7,
    Customer = 8,
    TechnologyAsset = 9
}

/// <summary>Spec section 6 relationship vocabulary.</summary>
public enum GraphRelationshipType
{
    WorksFor = 0,
    ReportsTo = 1,
    DependsOn = 2,
    Supplies = 3,
    Produces = 4,
    Consumes = 5,
    Stores = 6,
    Serves = 7,
    Uses = 8,
    Owns = 9,
    LocatedAt = 10,
    ContractedWith = 11,
    Blocks = 12,
    Affects = 13,
    Requires = 14,
    Replaces = 15,
    ConnectedTo = 16,
    GeneratesRevenue = 17
}
