using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.ContentReviews;
using BlogSpecLab.Core.Application.Posts;
using BlogSpecLab.Core.Domain.ContentReviews;
using BlogSpecLab.Core.Domain.Posts;
using BlogSpecLab.Tests.Api.Infrastructure;

namespace BlogSpecLab.Tests.Application;

/// <summary>
/// In-memory replacements for the content review service's dependencies; no external service is involved.
/// </summary>
internal sealed class ContentReviewServiceFixture
{
    public static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    public static readonly UserAccess AuthorA =
        new(Guid.NewGuid(), "Author A", IsActive: true, new HashSet<Role> { Role.Author });

    public static readonly UserAccess AuthorB =
        new(Guid.NewGuid(), "Author B", IsActive: true, new HashSet<Role> { Role.Author });

    public static readonly UserAccess EditorOnly =
        new(Guid.NewGuid(), "Editor 2", IsActive: true, new HashSet<Role> { Role.Editor });

    public ManualTimeProvider Time { get; } = new(Start.AddHours(1));

    public InMemoryPostStore Store { get; } = new();

    public FakeContentReviewModel Model { get; } = new();

    public ContentReviewDeadlineOptions Deadline { get; set; } = new();

    public ContentReviewService CreateService(UserAccess? user) =>
        new(
            new FixedCurrentUser(user?.UserId),
            new FixedUserAccessReader(AuthorA, AuthorB, EditorOnly),
            Store,
            Store,
            Model,
            new ContentReviewValidator(),
            Deadline,
            Time);

    public Post AddDraft(UserAccess author, string title = "عنوان پست", string body = "متن پست که می‌خواهیم بررسی شود.")
    {
        var post = Post.Create(author.UserId, author.DisplayName, title, body, Start);
        Store.Add(post);
        return post;
    }

    public Post AddReturned(UserAccess author)
    {
        var post = AddDraft(author);
        post.Submit(author.UserId, author.DisplayName, Start.AddMinutes(1));
        post.Return(EditorOnly.UserId, EditorOnly.DisplayName, "اصلاح لازم است", Start.AddMinutes(2));
        return post;
    }

    public Post AddInReview(UserAccess author)
    {
        var post = AddDraft(author);
        post.Submit(author.UserId, author.DisplayName, Start.AddMinutes(1));
        return post;
    }

    private sealed class FixedCurrentUser(Guid? userId) : ICurrentUser
    {
        public bool IsAuthenticated => userId is not null;

        public Guid? UserId => userId;
    }

    private sealed class FixedUserAccessReader(params UserAccess[] users) : IUserAccessReader
    {
        public Task<UserAccess?> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(users.FirstOrDefault(user => user.UserId == userId));
    }
}

/// <summary>Posts and reviews kept in memory. Saved reviews are only visible after <see cref="SaveChangesAsync"/>.</summary>
internal sealed class InMemoryPostStore : IPostRepository, IContentReviewRepository
{
    private readonly Dictionary<Guid, Post> _posts = [];
    private readonly List<ContentReview> _pending = [];
    private readonly List<ContentReview> _saved = [];

    public IReadOnlyList<ContentReview> SavedReviews => _saved;

    public int PostSaves { get; private set; }

    public void Delete(Guid postId)
    {
        _posts.Remove(postId);
        _saved.RemoveAll(review => review.PostId == postId);
    }

    public Task<Post?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_posts.GetValueOrDefault(id));

    public Task<IReadOnlyList<Post>> ListByAuthorAsync(Guid authorId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Post>>(_posts.Values.Where(post => post.AuthorId == authorId).ToList());

    public Task<IReadOnlyList<Post>> ListByStatusAsync(PostStatus status, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Post>> ListPublishedAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public void Add(Post post) => _posts[post.Id] = post;

    public void Remove(Post post) => _posts.Remove(post.Id);

    Task IPostRepository.SaveChangesAsync(CancellationToken cancellationToken)
    {
        PostSaves++;
        return Task.CompletedTask;
    }

    public void Add(ContentReview review) => _pending.Add(review);

    public Task<IReadOnlyList<ContentReview>> ListByPostAsync(Guid postId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ContentReview>>(
            _saved.Where(review => review.PostId == postId).OrderByDescending(review => review.CreatedAt).ToList());

    public Task<ReviewedPostState?> GetPostStateAsync(Guid postId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_posts.TryGetValue(postId, out var post) ? new ReviewedPostState(post.AuthorId, post.Version) : null);

    Task IContentReviewRepository.SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (_pending.Any(review => !_posts.ContainsKey(review.PostId)))
        {
            _pending.Clear();
            throw new ConcurrencyConflictException();
        }

        _saved.AddRange(_pending);
        _pending.Clear();
        return Task.CompletedTask;
    }
}
