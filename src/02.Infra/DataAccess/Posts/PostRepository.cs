using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Posts;
using BlogSpecLab.Core.Domain.Posts;
using Microsoft.EntityFrameworkCore;

namespace BlogSpecLab.Infra.DataAccess.Posts;

internal sealed class PostRepository(BlogSpecLabDbContext dbContext) : IPostRepository
{
    public Task<Post?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Posts
            .Include(post => post.StatusChanges)
            .SingleOrDefaultAsync(post => post.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Post>> ListByAuthorAsync(
        Guid authorId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Posts
            .AsNoTracking()
            .Where(post => post.AuthorId == authorId)
            .OrderByDescending(post => post.LastModifiedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Post>> ListByStatusAsync(
        PostStatus status,
        CancellationToken cancellationToken = default) =>
        await dbContext.Posts
            .AsNoTracking()
            .Where(post => post.Status == status)
            .OrderByDescending(post => post.LastModifiedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Post>> ListPublishedAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Posts
            .AsNoTracking()
            .Where(post => post.Status == PostStatus.Published)
            .OrderByDescending(post => post.LastPublishedAt)
            .ToListAsync(cancellationToken);

    public void Add(Post post) => dbContext.Posts.Add(post);

    public void Remove(Post post) => dbContext.Posts.Remove(post);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // Drop the stale tracked state so that a following read returns what is actually stored.
            dbContext.ChangeTracker.Clear();
            throw new ConcurrencyConflictException(exception);
        }
    }
}
