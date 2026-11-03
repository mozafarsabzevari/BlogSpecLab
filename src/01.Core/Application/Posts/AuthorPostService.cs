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
        var (post, _, error) = await GetOwnPostAsync(id, cancellationToken);

        return error is null
            ? Result.Success(ToDetail(post!))
            : Result.Failure<AuthorPostDetail>(error);
    }

    /// <summary>
    /// Edits do not depend on a seen version (FR-012). On a concurrent change the post is read again and the
    /// edit is applied once more to its current state; a second conflict is reported as stale (R-11).
    /// </summary>
    public async Task<Result<AuthorPostDetail>> EditAsync(
        Guid id,
        string? title,
        string? body,
        bool confirmRevertToDraft = false,
        CancellationToken cancellationToken = default)
    {
        const int maxAttempts = 2;

        for (var attempt = 1; ; attempt++)
        {
            var (post, author, error) = await GetOwnPostAsync(id, cancellationToken);

            if (error is not null)
            {
                return Result.Failure<AuthorPostDetail>(error);
            }

            var editError = ApplyEdit(post!, author!, title, body, confirmRevertToDraft);

            if (editError is not null)
            {
                return Result.Failure<AuthorPostDetail>(editError);
            }

            try
            {
                await posts.SaveChangesAsync(cancellationToken);
                return Result.Success(ToDetail(post!));
            }
            catch (ConcurrencyConflictException) when (attempt == maxAttempts)
            {
                return Result.Failure<AuthorPostDetail>(await ConflictErrorAsync(id, cancellationToken));
            }
            catch (ConcurrencyConflictException)
            {
                // Read the post again and re-apply the edit to its current state.
            }
        }
    }

    public Task<Result<AuthorPostDetail>> SubmitAsync(
        Guid id,
        Guid expectedVersion,
        CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(
            id,
            expectedVersion,
            (post, author, now) => post.Submit(author.UserId, author.DisplayName, now),
            cancellationToken);

    public Task<Result<AuthorPostDetail>> WithdrawAsync(
        Guid id,
        Guid expectedVersion,
        CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(
            id,
            expectedVersion,
            (post, author, now) => post.Withdraw(author.UserId, author.DisplayName, now),
            cancellationToken);

    public async Task<Result> DeleteAsync(Guid id, Guid expectedVersion, CancellationToken cancellationToken = default)
    {
        var (post, _, error) = await GetOwnPostAsync(id, cancellationToken);

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

    private async Task<Result<AuthorPostDetail>> ChangeStatusAsync(
        Guid id,
        Guid expectedVersion,
        Action<Post, UserAccess, DateTimeOffset> change,
        CancellationToken cancellationToken)
    {
        var (post, author, error) = await GetOwnPostAsync(id, cancellationToken);

        if (error is not null)
        {
            return Result.Failure<AuthorPostDetail>(error);
        }

        if (post!.Version != expectedVersion)
        {
            return Result.Failure<AuthorPostDetail>(StaleVersion(post));
        }

        try
        {
            change(post, author!, timeProvider.GetUtcNow());
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

    private async Task<(Post? Post, UserAccess? Author, Error? Error)> GetOwnPostAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var (author, accessError) = await GetAuthorAsync(cancellationToken);

        if (accessError is not null)
        {
            return (null, null, accessError);
        }

        var post = await posts.GetByIdAsync(id, cancellationToken);

        return post is not null && post.AuthorId == author!.UserId
            ? (post, author, null)
            : (null, null, NotFound());
    }

    private Error? ApplyEdit(Post post, UserAccess author, string? title, string? body, bool confirmRevertToDraft)
    {
        var now = timeProvider.GetUtcNow();

        try
        {
            if (post.Status is not (PostStatus.Approved or PostStatus.Published))
            {
                post.Edit(title, body, now);
            }
            else if (confirmRevertToDraft)
            {
                post.EditAndRevertToDraft(author.UserId, author.DisplayName, title, body, now);
            }
            else
            {
                return PostErrors.ConfirmationRequired(post);
            }
        }
        catch (PostDomainException exception)
        {
            return ToError(exception, post);
        }

        return null;
    }

    private async Task<Error> ConflictErrorAsync(Guid id, CancellationToken cancellationToken)
    {
        var current = await posts.GetByIdAsync(id, cancellationToken);

        return current is null ? NotFound() : StaleVersion(current);
    }

    private static Error NotFound() => PostErrors.NotFound();

    private static Error StaleVersion(Post post) => PostErrors.StaleVersion(post);

    private static Error ToError(PostDomainException exception, Post? post = null) =>
        PostErrors.FromDomain(exception, post);

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
