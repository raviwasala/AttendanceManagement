namespace AttendanceSystem.Common.Exceptions;

/// <summary>Exception thrown when a requested entity is not found.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string entityName, int id)
        : base($"{entityName} with ID {id} was not found.") { }

    public NotFoundException(string message) : base(message) { }
}

/// <summary>Exception thrown when a business rule is violated.</summary>
public class BusinessException : Exception
{
    public BusinessException(string message) : base(message) { }
}

/// <summary>Exception thrown for duplicate key violations.</summary>
public class DuplicateException : Exception
{
    public DuplicateException(string field, string value)
        : base($"A record with {field} '{value}' already exists.") { }
}

/// <summary>Exception thrown for unauthorised access attempts.</summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "Access denied.") : base(message) { }
}
