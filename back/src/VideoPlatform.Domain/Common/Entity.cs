// File: Entity.cs
// Purpose: Base class for every entity in the Domain layer.
// Logic/Approach: Gives every entity a strongly-typed Guid identity and
// value-based equality (two entities are equal when their Id matches),
// which is the standard DDD definition of entity equality: entities are
// compared by identity, never by the values of their properties.
namespace VideoPlatform.Domain.Common;

public abstract class Entity
{
    // The entity's unique identity. Protected setter: only a derived entity,
    // through its own constructor or static factory method, may assign it.
    public Guid Id { get; protected set; }

    // Needed by EF Core to materialize entities from query results.
    protected Entity()
    {
    }

    protected Entity(Guid id)
    {
        Id = id;
    }

    // Two entities of the same runtime type with the same Id are the same entity.
    public override bool Equals(object? obj)
    {
        if (obj is not Entity other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        return Id == other.Id;
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity? left, Entity? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Entity? left, Entity? right) => !(left == right);
}
