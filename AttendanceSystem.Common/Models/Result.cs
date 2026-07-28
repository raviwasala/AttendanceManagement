namespace AttendanceSystem.Common.Models;

/// <summary>Represents the outcome of a service operation.</summary>
public class Result
{
    public bool IsSuccess { get; protected set; }
    public string? ErrorMessage { get; protected set; }
    public List<string> Errors { get; protected set; } = new();

    public static Result Success() => new() { IsSuccess = true };
    public static Result Failure(string error) => new() { IsSuccess = false, ErrorMessage = error };
    public static Result Failure(List<string> errors) => new() { IsSuccess = false, Errors = errors, ErrorMessage = string.Join("; ", errors) };
}

/// <summary>Represents the outcome of a service operation with a typed payload.</summary>
public class Result<T> : Result
{
    public T? Data { get; private set; }

    public static Result<T> Success(T data) => new() { IsSuccess = true, Data = data };
    public new static Result<T> Failure(string error) => new() { IsSuccess = false, ErrorMessage = error };
    public new static Result<T> Failure(List<string> errors) => new() { IsSuccess = false, Errors = errors, ErrorMessage = string.Join("; ", errors) };
}
