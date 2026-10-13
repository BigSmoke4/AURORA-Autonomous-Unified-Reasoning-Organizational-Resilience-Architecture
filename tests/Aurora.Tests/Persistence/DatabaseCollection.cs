namespace Aurora.Tests.Persistence;

/// <summary>
/// Every test that touches the shared PostgreSQL database belongs to this collection. Those tests drop
/// and recreate the database, so they must never run in parallel with each other.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DatabaseCollection
{
    public const string Name = "Database";
}
