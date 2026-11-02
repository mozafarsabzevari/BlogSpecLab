using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Domain.Posts;

namespace BlogSpecLab.Core.Application.Posts;

/// <summary>
/// Author use cases. The Author role is read from the server-side source on every call, and posts of other
/// authors are reported as not found.
/// </summary>
public sealed class AuthorPostService(
    ICurrentUser currentUser,
    IUserAccessReader userAccessReader,
    IPostRepository posts,
    TimeProvider timeProvider)
{
    public async Task<Result<AuthorPostDetail>> CreateAsync(
        string? title,
        string? body,
        CancellationToken cancellationToken = default)
    {
        var (author, accessError) = await GetAuthorAsync(cancellationToken);

        if (accessError is not null)
        {
            return Result.Failure<AuthorPostDetail>(accessError);
        }

        Post post;

        try
        {
            post = Post.Create(author!.UserId, author.DisplayName, title, body, timeProvider.GetUtcNow());
        }
        catch (PostDomainException exception)
        {
            return Result.Failure<AuthorPostDetail>(ToError(exception));
        }

        posts.Add(post);
        await posts.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDetail(post));
    }

    public async Task<Result<IReadOnlyList<AuthorPostSummary>>> ListAsync(CancellationToken cancellationToken = default)
    {
        var (author, accessError) = await GetAuthorAsync(cancellationToken);

        if (accessError is not null)
        {
            return Result.Failure<IReadOnlyList<AuthorPostSummary>>(accessError);
        }

        var ownPosts = await posts.ListByAuthorAsync(author!.UserId, cancellationToken);

        return Result.Success<IReadOnlyList<AuthorPostSummary>>(ownPosts.Select(ToSummary).ToList());
    }

    public async Task<Result<AuthorPostDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var (post, error) = await GetOwnPostAsync(id, cancellationToken);

        return error is null
            ? Result.Success(ToDetail(post!))
            : Result.Failure<AuthorPostDetail>(error);
    }

    public async Task<Result<AuthorPostDetail>> EditAsync(
        Guid id,
        string? title,
        string? body,
        CancellationToken cancellationToken = default)
    {
        var (post, error) = await GetOwnPostAsync(id, cancellationToken);

        if (error is not null)
        {
            return Result.Failure<AuthorPostDetail>(error);
        }

        try
        {
            post!.Edit(title, body, timeProvider.GetUtcNow());
        }
        catch (PostDomainException exception)
        {
            return Result.Failure<AuthorPostDetail>(ToError(exception, post));
        }

        try
        {
            await posts.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result.Failure<AuthorPostDetail>(await ConflictErrorAsync(id, cancellationToken));
        }

        return Result.Success(ToDetail(post));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid expectedVersion, CancellationToken cancellationToken = default)
    {
        var (post, error) = await GetOwnPostAsync(id, cancellationToken);

        if (error is not null)
        {
            return Result.Failure(error);
        }

        if (post!.Version != expectedVersion)
        {
            return Result.Failure(StaleVersion(post));
        }

        try
        {
            post.EnsureCanDelete();
        }
        catch (PostDomainException exception)
        {
            return Result.Failure(ToError(exception, post));
        }

        posts.Remove(post);

        try
        {
            await posts.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result.Failure(await ConflictErrorAsync(id, cancellationToken));
        }

        return Result.Success();
    }

    private async Task<(UserAccess? Author, Error? Error)> GetAuthorAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return (null, new Error(ErrorCode.Unauthenticated, "ورود به سامانه لازم است."));
        }

        var access = await userAccessReader.GetAsync(userId, cancellationToken);

        return access is not null && access.HasRole(Role.Author)
            ? (access, null)
            : (null, new Error(ErrorCode.Forbidden, "این عملیات فقط برای نویسندگان مجاز است."));
    }

    private async Task<(Post? Post, Error? Error)> GetOwnPostAsync(Guid id, CancellationToken cancellationToken)
    {
        var (author, accessError) = await GetAuthorAsync(cancellationToken);

        if (accessError is not null)
        {
            return (null, accessError);
        }

        var post = await posts.GetByIdAsync(id, cancellationToken);

        return post is not null && post.AuthorId == author!.UserId
            ? (post, null)
            : (null, NotFound());
    }

    private async Task<Error> ConflictErrorAsync(Guid id, CancellationToken cancellationToken)
    {
        var current = await posts.GetByIdAsync(id, cancellationToken);

        return current is null ? NotFound() : StaleVersion(current);
    }

    private static Error NotFound() => new(ErrorCode.NotFound, "پست پیدا نشد.");

    private static Error StaleVersion(Post post) =>
        new(
            ErrorCode.StaleVersion,
            "پست پس از آخرین مشاهدهٔ شما تغییر کرده است. آن را دوباره بخوانید.",
            post.Status.ToString(),
            post.Version);

    private static Error ToError(PostDomainException exception, Post? post = null)
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

    private static AuthorPostSummary ToSummary(Post post) =>
        new(
            post.Id,
            post.Title,
            post.Status,
            post.LastReturnReason,
            post.LastModifiedAt,
            post.LastPublishedAt,
            post.Version);

    private static AuthorPostDetail ToDetail(Post post) =>
        new(
            post.Id,
            post.Title,
            post.Body,
            post.Status,
            post.LastReturnReason,
            post.CreatedAt,
            post.LastModifiedAt,
            post.LastPublishedAt,
            post.Version);
}
