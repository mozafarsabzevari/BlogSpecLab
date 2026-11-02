using System.Diagnostics.CodeAnalysis;

namespace BlogSpecLab.Core.Application.Common;

public enum ErrorCode
{
    NotFound,
    Forbidden,
    Unauthenticated,
    StaleVersion,
    ConfirmationRequired,
    InvalidStatusTransition,
    DeleteNotAllowed,
    SelfApprovalNotAllowed,
    InvalidTitle,
    InvalidBody,
    ReturnReasonRequired
}

/// <summary>
/// <paramref name="CurrentStatus"/> and <paramref name="CurrentVersion"/> are set only after the caller's
/// access to the post has been verified, and never carry post content.
/// </summary>
public sealed record Error(
    ErrorCode Code,
    string Message,
    string? CurrentStatus = null,
    Guid? CurrentVersion = null);

public class Result
{
    protected Result(Error? error)
    {
        Error = error;
    }

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);
}

public sealed class Result<T> : Result
{
    private Result(T? value, Error? error)
        : base(error)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(value, null);

    public static new Result<T> Failure(Error error) => new(default, error);
}
