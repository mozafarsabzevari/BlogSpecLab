using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Posts;
using BlogSpecLab.Core.Domain.ContentReviews;
using BlogSpecLab.Core.Domain.Posts;

namespace BlogSpecLab.Core.Application.ContentReviews;

/// <summary>
/// AI review of an author's own post. The review is advisory only: it reads the post and stores a separate
/// result, and never changes the post or its history (FR-009, FR-010).
/// </summary>
public sealed class ContentReviewService(
    ICurrentUser currentUser,
    IUserAccessReader userAccessReader,
    IPostRepository posts,
    IContentReviewRepository reviews,
    IContentReviewModel model,
    ContentReviewValidator validator,
    ContentReviewDeadlineOptions deadline,
    TimeProvider timeProvider)
{
    /// <exception cref="OperationCanceledException">The client cancelled the request; nothing is stored.</exception>
    public async Task<Result<ContentReviewDetail>> RequestAsync(
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var (author, accessError) = await GetAuthorAsync(cancellationToken);

        if (accessError is not null)
        {
            return Result.Failure<ContentReviewDetail>(accessError);
        }

        var post = await posts.GetByIdAsync(postId, cancellationToken);

        if (post is null || post.AuthorId != author!.UserId)
        {
            return Result.Failure<ContentReviewDetail>(PostErrors.NotFound());
        }

        if (post.Status is not (PostStatus.Draft or PostStatus.Returned))
        {
            return Result.Failure<ContentReviewDetail>(ContentReviewErrors.NotAvailable(post.Status));
        }

        var snapshot = new ContentReviewSnapshot(
            post.Id, post.AuthorId, post.Title, post.Body, post.Version, post.LastModifiedAt);

        var (outcome, deadlineError) = await CallModelAsync(snapshot, cancellationToken);

        if (deadlineError is not null)
        {
            return Result.Failure<ContentReviewDetail>(deadlineError);
        }

        var validated = outcome switch
        {
            ContentReviewModelOutcome.Candidate candidate
                => validator.Validate(candidate.Review, snapshot, timeProvider.GetUtcNow()),
            ContentReviewModelOutcome.Refused
                => Result.Failure<ContentReview>(ContentReviewErrors.Refused()),
            ContentReviewModelOutcome.ProviderUnavailable
                => Result.Failure<ContentReview>(ContentReviewErrors.ServiceUnavailable()),
            _ => Result.Failure<ContentReview>(ContentReviewErrors.OutputInvalid())
        };

        if (!validated.IsSuccess)
        {
            return Result.Failure<ContentReviewDetail>(validated.Error);
        }

        // The post may have been deleted while the model was working (research.md, R-11). An edit in the
        // meantime does not matter: the review belongs to the version it was made for.
        var current = await reviews.GetPostStateAsync(postId, cancellationToken);

        if (current is null || current.AuthorId != author.UserId)
        {
            return Result.Failure<ContentReviewDetail>(PostErrors.NotFound());
        }

        reviews.Add(validated.Value!);

        try
        {
            await reviews.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result.Failure<ContentReviewDetail>(PostErrors.NotFound());
        }

        return Result.Success(ContentReviewDetail.From(validated.Value!, current.Version));
    }

    /// <summary>Newest first. Available in every post status (FR-013).</summary>
    public async Task<Result<IReadOnlyList<ContentReviewDetail>>> ListAsync(
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var (author, accessError) = await GetAuthorAsync(cancellationToken);

        if (accessError is not null)
        {
            return Result.Failure<IReadOnlyList<ContentReviewDetail>>(accessError);
        }

        var post = await reviews.GetPostStateAsync(postId, cancellationToken);

        if (post is null || post.AuthorId != author!.UserId)
        {
            return Result.Failure<IReadOnlyList<ContentReviewDetail>>(PostErrors.NotFound());
        }

        var stored = await reviews.ListByPostAsync(postId, cancellationToken);

        return Result.Success<IReadOnlyList<ContentReviewDetail>>(
            stored.Select(review => ContentReviewDetail.From(review, post.Version)).ToList());
    }

    /// <summary>
    /// Applies the deadline on top of the client's cancellation. Reaching the deadline is an error result; a
    /// cancelled client request propagates as <see cref="OperationCanceledException"/>.
    /// </summary>
    private async Task<(ContentReviewModelOutcome? Outcome, Error? DeadlineError)> CallModelAsync(
        ContentReviewSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        using var deadlineSource = new CancellationTokenSource(deadline.Timeout, timeProvider);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, deadlineSource.Token);

        try
        {
            var outcome = await model.ReviewAsync(
                new ContentReviewInput(snapshot.Title, snapshot.Body), linkedSource.Token);

            return (outcome, null);
        }
        catch (OperationCanceledException)
            when (deadlineSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return (null, ContentReviewErrors.TimedOut());
        }
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
}
