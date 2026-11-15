// File: IQueryHandler.cs
// Purpose: The read-only counterpart to ICommandHandler — same reasoning,
// kept as a separate interface so a handler's name alone (ICommandHandler
// vs IQueryHandler) documents whether it mutates state.
namespace VideoPlatform.Application.Common.Messaging;

public interface IQueryHandler<in TQuery, TResult>
{
    Task<TResult> Handle(TQuery query, CancellationToken cancellationToken = default);
}
