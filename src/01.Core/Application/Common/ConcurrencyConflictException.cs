namespace BlogSpecLab.Core.Application.Common;

public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("The record was changed by another operation.")
    {
    }

    public ConcurrencyConflictException(Exception innerException)
        : base("The record was changed by another operation.", innerException)
    {
    }
}
