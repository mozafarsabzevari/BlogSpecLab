using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Domain.Posts;

namespace BlogSpecLab.Core.Application.Posts;

/// <summary>
/// Editor use cases. The Editor role is read from the server-side source on every call. Draft and Returned
/// posts are hidden from editors except through their status history (FR-011, R-13).
/// </summary>
public sealed class EditorPostService(
    ICurrentUser currentUser,
    IUserAccessReader userAccessReader,
    IPostRepository posts,
    TimeProvider timeProvider)
{
    public static IReadOnlySet<PostStatus> VisibleStatuses { get; } =
        new HashSet<PostStatus> { PostStatus.InReview, PostStatus.Approved, PostStatus.Published };

    public async Task<Result<IReadOnlyList<EditorPostSummary>>> ListAsync(
        PostStatus status,
        CancellationToken cancellationToken = default)
    {
        if (!VisibleStatuses.Contains(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Editors can only list visible statuses.");
        }

        var (_, accessError) = await GetEditorAsync(cancellationToken);

        if (accessError is not null)
        {
            return Result.Failure<IReadOnlyList<EditorPostSummary>>(accessError);
        }

        var matching = await posts.ListByStatusAsync(status, cancellationToken);

        return Result.Success<IReadOnlyList<EditorPostSummary>>(matching.Select(ToSummary).ToList());
    }

    public async Task<Result<EditorPostDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var (post, _, error) = await GetVisiblePostAsync(id, cancellationToken);

        return error is null
            ? Result.Success(ToDetail(post!))
            : Result.Failure<EditorPostDetail>(error);
    }

    public async Task<Result<EditorPostHistory>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var (_, accessError) = await GetEditorAsync(cancellationToken);

        if (accessError is not null)
        {
            return Result.Failure<EditorPostHistory>(accessError);
        }

        var post = await posts.GetByIdAsync(id, cancellationToken);

        return post is null
            ? Result.Failure<EditorPostHistory>(PostErrors.NotFound())
            : Result.Success(new EditorPostHistory(post.Id, post.Status, ToHistory(post)));
    }

    public Task<Result<EditorPostDetail>> ApproveAsync(
        Guid id,
        Guid expectedVersion,
        CancellationToken cancellationToken = default) =>
        DecideAsync(
            id,
            expectedVersion,
            (post, editor, now) => post.Approve(editor.UserId, editor.DisplayName, now),
            cancellationToken);

    public Task<Result<EditorPostDetail>> ReturnAsync(
        Guid id,
        Guid expectedVersion,
        string? reason,
        CancellationToken cancellationToken = default) =>
        DecideAsync(
            id,
            expectedVersion,
            (post, editor, now) => post.Return(editor.UserId, editor.DisplayName, reason, now),
            cancellationToken);

    private async Task<Result<EditorPostDetail>> DecideAsync(
        Guid id,
        Guid expectedVersion,
        Action<Post, UserAccess, DateTimeOffset> decide,
        CancellationToken cancellationToken)
    {
        var (post, editor, error) = await GetVisiblePostAsync(id, cancellationToken);

        if (error is not null)
        {
            return Result.Failure<EditorPostDetail>(error);
        }

        if (post!.Version != expectedVersion)
        {
            return Result.Failure<EditorPostDetail>(PostErrors.StaleVersion(post));
        }

        try
        {
            decide(post, editor!, timeProvider.GetUtcNow());
        }
        catch (PostDomainException exception)
        {
            return Result.Failure<EditorPostDetail>(PostErrors.FromDomain(exception, post));
        }

        try
        {
            await posts.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            var current = await posts.GetByIdAsync(id, cancellationToken);

            return Result.Failure<EditorPostDetail>(
                current is null ? PostErrors.NotFound() : PostErrors.StaleVersion(current));
        }

        return Result.Success(ToDetail(post));
    }

    private async Task<(UserAccess? Editor, Error? Error)> GetEditorAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return (null, new Error(ErrorCode.Unauthenticated, "ورود به سامانه لازم است."));
        }

        var access = await userAccessReader.GetAsync(userId, cancellationToken);

        return access is not null && access.HasRole(Role.Editor)
            ? (access, null)
            : (null, new Error(ErrorCode.Forbidden, "این عملیات فقط برای ویراستاران مجاز است."));
    }

    private async Task<(Post? Post, UserAccess? Editor, Error? Error)> GetVisiblePostAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var (editor, accessError) = await GetEditorAsync(cancellationToken);

        if (accessError is not null)
        {
            return (null, null, accessError);
        }

        var post = await posts.GetByIdAsync(id, cancellationToken);

        return post is not null && VisibleStatuses.Contains(post.Status)
            ? (post, editor, null)
            : (null, null, PostErrors.NotFound());
    }

    private static IReadOnlyList<PostHistoryEntry> ToHistory(Post post) =>
        post.StatusChanges
            .OrderBy(change => change.OccurredAt)
            .Select(change => new PostHistoryEntry(
                change.ActorId,
                change.ActorDisplayName,
                change.OccurredAt,
                change.FromStatus,
                change.ToStatus,
                change.Reason))
            .ToList();

    private static EditorPostSummary ToSummary(Post post) =>
        new(
            post.Id,
            post.Title,
            post.AuthorDisplayName,
            post.Status,
            post.LastModifiedAt,
            post.LastPublishedAt,
            post.Version);

    private static EditorPostDetail ToDetail(Post post) =>
        new(
            post.Id,
            post.Title,
            post.Body,
            post.AuthorId,
            post.AuthorDisplayName,
            post.Status,
            post.CreatedAt,
            post.LastModifiedAt,
            post.LastPublishedAt,
            post.Version,
            ToHistory(post));
}
