namespace Ayush360.Platform.Errors;

/// <summary>
/// Expected business errors. Throw these from any API; GlobalExceptionHandler turns them into
/// { message, errors?, correlationId } with the right HTTP status. The message is shown to the user.
/// </summary>
public abstract class AppException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

/// <summary>400: field errors, keyed by the camelCase field name the frontend uses (e.g. "mobileNumber").</summary>
public sealed class AppValidationException(string message, IReadOnlyDictionary<string, string> errors)
    : AppException(message, 400)
{
    public AppValidationException(string field, string error)
        : this(error, new Dictionary<string, string> { [field] = error })
    {
    }

    public IReadOnlyDictionary<string, string> Errors { get; } = errors;
}

/// <summary>404: missing, or belongs to another hospital (never reveal which).</summary>
public sealed class NotFoundException(string message = "Record not found.") : AppException(message, 404);

/// <summary>403: signed in but not allowed.</summary>
public sealed class ForbiddenException(string message = "You don't have permission to do this.") : AppException(message, 403);

/// <summary>409: e.g. duplicate patient, visit already closed, stale update.</summary>
public sealed class ConflictException(string message) : AppException(message, 409);

/// <summary>400 with a message only (no field errors), e.g. "Invalid username or password".</summary>
public sealed class BadRequestException(string message) : AppException(message, 400);

/// <summary>401: not signed in, or the session (refresh token) is no longer valid.</summary>
public sealed class UnauthorizedException(string message = "Your session has expired. Please sign in again.")
    : AppException(message, 401);
