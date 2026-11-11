using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.ContentReviews;
using BlogSpecLab.Core.Domain.ContentReviews;
using Microsoft.EntityFrameworkCore;

namespace BlogSpecLab.Infra.DataAccess.ContentReviews;

internal sealed class ContentReviewRepository(BlogSpecLabDbContext dbContext) : IContentReviewRepository
{
    public void Add(ContentReview review) => dbContext.ContentReviews.Add(review);

    public async Task<IReadOnlyList<ContentReview>> ListByPostAsync(
        Guid postId,
        CancellationToken cancellationToken = default) =>
        await dbContext.ContentReviews
            .AsNoTracking()
            .Where(review => review.PostId == postId)
            .OrderByDescending(review => review.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<ReviewedPostState?> GetPostStateAsync(Guid postId, CancellationToken cancellationToken = default) =>
        dbContext.Posts
            .AsNoTracking()
            .Where(post => post.Id == postId)
            .Select(post => new ReviewedPostState(post.AuthorId, post.Version))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            // The only expected failure is the foreign key to a post deleted after the last check.
            dbContext.ChangeTracker.Clear();
            throw new ConcurrencyConflictException(exception);
        }
    }
}
