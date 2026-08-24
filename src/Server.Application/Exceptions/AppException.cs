namespace ResellerSystem.Server.Application.Exceptions;

/// <summary>Base for all application-level exceptions that carry a stable error code.</summary>
public abstract class AppException : Exception
{
    protected AppException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string code, string message) : base(code, message) { }
}

public sealed class ValidationFailedException : AppException
{
    public ValidationFailedException(IReadOnlyList<string> details)
        : base("VALIDATION_FAILED", "One or more validation errors occurred.")
    {
        Details = details;
    }

    public IReadOnlyList<string> Details { get; }
}

public sealed class ConflictException : AppException
{
    public ConflictException(string code, string message) : base(code, message) { }
}

public sealed class DatabaseNotReadyException : AppException
{
    public DatabaseNotReadyException(string message)
        : base("DATABASE_NOT_READY", message) { }
}
