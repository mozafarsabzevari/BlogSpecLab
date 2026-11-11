using BlogSpecLab.Core.Domain.ContentReviews;

namespace BlogSpecLab.Core.Application.ContentReviews;

public interface IContentReviewRepository
{
    void Add(ContentReview review);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<ContentReview>> ListByPostAsync(Guid postId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the stored owner and version of a post without tracking it, so that the result reflects changes made
    /// by other requests. Returns null when the post does not exist.
    /// </summary>
    Task<ReviewedPostState?> GetPostStateAsync(Guid postId, CancellationToken cancellationToken = default);

    /// <exception cref="Common.ConcurrencyConflictException">The post was deleted before the review was stored.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record ReviewedPostState(Guid AuthorId, Guid Version);
