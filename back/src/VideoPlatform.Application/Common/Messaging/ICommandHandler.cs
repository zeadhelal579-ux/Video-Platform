// File: ICommandHandler.cs
// Purpose: The single contract every Command handler implements. Api
// endpoints depend on this interface (resolved through DI) instead of
// calling concrete handler classes directly — this is what keeps an
// endpoint method to about three lines (ARCHITECTURE.md section 2.4).
// Note: not listed as its own file in ARCHITECTURE.md — added as a
// minimal, framework-free stand-in for a mediator library. A plain
// interface was chosen over MediatR/similar because TOOLS.md lists no
// mediator dependency and the project has no cross-cutting pipeline
// behaviors (validation, logging) yet that would justify pulling one in.
namespace VideoPlatform.Application.Common.Messaging;

public interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> Handle(TCommand command, CancellationToken cancellationToken = default);
}
