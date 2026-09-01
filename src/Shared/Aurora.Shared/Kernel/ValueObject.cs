namespace Aurora.Shared.Kernel;

/// <summary>Base class for immutable value objects compared by their component values, not identity.</summary>
public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is not ValueObject other || other.GetType() != GetType()) return false;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode() =>
        GetEqualityComponents().Aggregate(17, (hash, component) => hash * 31 + (component?.GetHashCode() ?? 0));
}
