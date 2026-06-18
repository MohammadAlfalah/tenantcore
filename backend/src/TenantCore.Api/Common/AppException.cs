namespace TenantCore.Api.Common;

/// <summary>
/// Base class for expected, business-level failures. Each carries the HTTP status code it should
/// map to, so the global exception handler can translate it without controllers writing try/catch.
/// </summary>
public abstract class AppException : Exception
{
    public abstract int StatusCode { get; }
    protected AppException(string message) : base(message) { }
}

public sealed class NotFoundException : AppException
{
    public override int StatusCode => StatusCodes.Status404NotFound;
    public NotFoundException(string message = "Resource not found.") : base(message) { }
}

public sealed class ConflictException : AppException
{
    public override int StatusCode => StatusCodes.Status409Conflict;
    public ConflictException(string message) : base(message) { }
}

public sealed class ValidationException : AppException
{
    public override int StatusCode => StatusCodes.Status400BadRequest;
    public ValidationException(string message) : base(message) { }
}

public sealed class ForbiddenException : AppException
{
    public override int StatusCode => StatusCodes.Status403Forbidden;
    public ForbiddenException(string message = "You do not have permission to perform this action.") : base(message) { }
}

public sealed class UnauthorizedAppException : AppException
{
    public override int StatusCode => StatusCodes.Status401Unauthorized;
    public UnauthorizedAppException(string message = "Invalid credentials.") : base(message) { }
}
