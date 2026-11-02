using BlogSpecLab.Core.Domain.Posts;

namespace BlogSpecLab.Core.Application.Posts;

public interface IPostRepository
{
    Task<Post?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Most recently changed first.</summary>
    Task<IReadOnlyList<Post>> ListByAuthorAsync(Guid authorId, CancellationToken cancellationToken = default);

    /// <summary>Most recently changed first.</summary>
    Task<IReadOnlyList<Post>> ListByStatusAsync(PostStatus status, CancellationToken cancellationToken = default);

    /// <summary>Most recently published first.</summary>
    Task<IReadOnlyList<Post>> ListPublishedAsync(CancellationToken cancellationToken = default);

    void Add(Post post);

    void Remove(Post post);

    /// <summary>
    /// Saves the post and its status history atomically.
    /// </summary>
    /// <exception cref="Common.ConcurrencyConflictException">
    /// The post was changed or deleted by another operation since it was read. Posts read afterwards reflect the
    /// latest stored state.
    /// </exception>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
