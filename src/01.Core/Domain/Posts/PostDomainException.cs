namespace BlogSpecLab.Core.Domain.Posts;

public enum PostErrorCode
{
    InvalidTitle,
    InvalidBody,
    ReturnReasonRequired,
    InvalidStatusTransition,
    SelfApprovalNotAllowed,
    DeleteNotAllowed
}

public sealed class PostDomainException(PostErrorCode code, string message) : Exception(message)
{
    public PostErrorCode Code { get; } = code;
}
