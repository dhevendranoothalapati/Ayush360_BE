using System.Data.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ayush360.Platform.Errors;

/// <summary>
/// Converts every exception into an <see cref="ErrorResponse"/>.
/// Unexpected errors are logged with the stack trace; the user only sees a reference number.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        int status;
        string message;
        IReadOnlyDictionary<string, string>? errors = null;

        switch (exception)
        {
            case AppValidationException validation:
                status = validation.StatusCode;
                message = validation.Message;
                errors = validation.Errors;
                break;
            case AppException app:
                status = app.StatusCode;
                message = app.Message;
                break;
            case BadHttpRequestException bad:
                status = bad.StatusCode;
                message = "The request was not valid.";
                break;
            case Exception when exception.InnerException is DbException { SqlState: "22001" }:
                // PostgreSQL "value too long for type character varying(n)": a field without its own length check.
                status = StatusCodes.Status400BadRequest;
                message = "One of the entries is too long. Please shorten it and try again.";
                break;
            case OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested:
                // Client went away; nothing to send.
                return true;
            default:
                status = StatusCodes.Status500InternalServerError;
                message = "Something went wrong. If it keeps happening, share this reference with support: "
                          + httpContext.TraceIdentifier;
                break;
        }

        if (status >= 500)
        {
            logger.LogError(exception, "Unhandled exception");
        }
        else
        {
            logger.LogWarning("Request failed with {StatusCode}: {Reason}", status, message);
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            new ErrorResponse(message, errors, httpContext.TraceIdentifier),
            cancellationToken);
        return true;
    }
}
