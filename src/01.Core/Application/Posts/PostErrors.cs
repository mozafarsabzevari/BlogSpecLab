using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Domain.Posts;

namespace BlogSpecLab.Core.Application.Posts;

internal static class PostErrors
{
    public static Error NotFound() => new(ErrorCode.NotFound, "پست پیدا نشد.");

    public static Error StaleVersion(Post post) =>
        new(
            ErrorCode.StaleVersion,
            "پست پس از آخرین مشاهدهٔ شما تغییر کرده است. آن را دوباره بخوانید.",
            post.Status.ToString(),
            post.Version);

    public static Error FromDomain(PostDomainException exception, Post? post = null)
    {
        var code = exception.Code switch
        {
            PostErrorCode.InvalidTitle => ErrorCode.InvalidTitle,
            PostErrorCode.InvalidBody => ErrorCode.InvalidBody,
            PostErrorCode.ReturnReasonRequired => ErrorCode.ReturnReasonRequired,
            PostErrorCode.InvalidStatusTransition => ErrorCode.InvalidStatusTransition,
            PostErrorCode.SelfApprovalNotAllowed => ErrorCode.SelfApprovalNotAllowed,
            PostErrorCode.DeleteNotAllowed => ErrorCode.DeleteNotAllowed,
            _ => throw new ArgumentOutOfRangeException(nameof(exception), exception.Code, "Unmapped domain error.")
        };

        return new Error(code, exception.Message, post?.Status.ToString(), post?.Version);
    }
}
