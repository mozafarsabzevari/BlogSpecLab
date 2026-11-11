namespace BlogSpecLab.Core.Domain.ContentReviews;

public enum ContentReviewErrorCode
{
    InvalidIssue,
    InvalidMissingInformation,
    InconsistentOutcome,
    InvalidMetadata
}

public sealed class ContentReviewDomainException(ContentReviewErrorCode code, string message) : Exception(message)
{
    public ContentReviewErrorCode Code { get; } = code;
}
