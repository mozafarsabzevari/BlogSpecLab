using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Domain.Posts;

namespace BlogSpecLab.Core.Application.Posts;

/// <summary>
/// Reader use cases; no sign-in needed. Only published posts are visible, and every other post is reported
/// exactly like a missing one (R-13, SC-003).
/// </summary>
public sealed class PublicPostQueries(IPostRepository posts)
{
    public async Task<IReadOnlyList<PublicPostSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var published = await posts.ListPublishedAsync(cancellationToken);

        return published
            .Select(post => new PublicPostSummary(post.Id, post.Title, post.AuthorDisplayName, PublishedAt(post)))
            .ToList();
    }

    public async Task<Result<PublicPostDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var post = await posts.GetByIdAsync(id, cancellationToken);

        return post is { Status: PostStatus.Published }
            ? Result.Success(new PublicPostDetail(
                post.Id, post.Title, post.Body, post.AuthorDisplayName, PublishedAt(post)))
            : Result.Failure<PublicPostDetail>(PostErrors.NotFound());
    }

    private static DateTimeOffset PublishedAt(Post post) =>
        post.LastPublishedAt ?? throw new InvalidOperationException($"Published post {post.Id} has no publication time.");
}
