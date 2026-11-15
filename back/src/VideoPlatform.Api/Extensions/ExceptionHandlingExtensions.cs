// File: ExceptionHandlingExtensions.cs
// Purpose: One place that turns an unhandled exception into an HTTP response,
// shaped like every other error body this Api returns (a bare JSON string),
// so the frontend's error handling needs no special case for it.
namespace VideoPlatform.Api.Extensions;

using Microsoft.AspNetCore.Diagnostics;
using VideoPlatform.Domain.Exceptions;

public static class ExceptionHandlingExtensions
{
    // Registers the global exception handler. ExceptionHandlerMiddleware has
    // already logged the exception by the time this callback runs.
    public static IApplicationBuilder UseApiExceptionHandler(this IApplicationBuilder app) =>
        app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
        {
            var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

            // A business-rule violation that slipped past a handler's own
            // checks (e.g. an illegal status change) conflicts with the
            // resource's current state — it is not a server fault. Anything
            // else is unexpected, and its details must never reach the client.
            var (statusCode, message) = exception is DomainException
                ? (StatusCodes.Status409Conflict, exception.Message)
                : (StatusCodes.Status500InternalServerError, "An unexpected error occurred.");

            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsJsonAsync(message);
        }));
}
