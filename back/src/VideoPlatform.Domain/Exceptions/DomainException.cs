// File: DomainException.cs
// Purpose: Base type for every exception raised from *inside* an entity
// method when a business invariant is violated (e.g. calling MarkReady()
// on a Video that is already Failed). Reserved for programming errors —
// expected, "normal" failures are represented with Result instead.
namespace VideoPlatform.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}

// Thrown when an entity method is invoked while the entity is in a status
// that does not legally allow that transition.
public sealed class InvalidStatusTransitionException : DomainException
{
    public InvalidStatusTransitionException(string entityName, string fromStatus, string toStatus)
        : base($"{entityName} cannot transition from '{fromStatus}' to '{toStatus}'.")
    {
    }
}
