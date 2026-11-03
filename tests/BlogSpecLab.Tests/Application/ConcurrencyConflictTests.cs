using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Posts;
using BlogSpecLab.Core.Domain.Posts;
using BlogSpecLab.Tests.Api.Infrastructure;

namespace BlogSpecLab.Tests.Application;

public class ConcurrencyConflictTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private static readonly UserAccess Author =
        new(Guid.NewGuid(), "Author A", IsActive: true, new HashSet<Role> { Role.Author });

    private static readonly UserAccess Editor =
        new(Guid.NewGuid(), "Editor 2", IsActive: true, new HashSet<Role> { Role.Editor });

    private readonly ManualTimeProvider _time = new(Start.AddHours(1));

    // FR-012, T053
    [Fact]
    public async Task AuthorEdit_AfterConcurrentPublish_IsRetriedOnceAndRevertsToDraft()
    {
        var seenByAuthor = CreateApproved();
        var publishedMeanwhile = CreatePublished();
        var repository = new ConflictingPostRepository(seenByAuthor, publishedMeanwhile);
        var service = CreateAuthorService(repository);

        var result = await service.EditAsync(seenByAuthor.Id, "New title", "New body", confirmRevertToDraft: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(PostStatus.Draft, result.Value!.Status);
        Assert.Equal(2, repository.SaveAttempts);
        Assert.Equal(1, repository.CommittedSaves);
        Assert.Same(publishedMeanwhile, repository.Current);
        Assert.Equal(PostStatus.Draft, publishedMeanwhile.Status);
        Assert.Equal("New title", publishedMeanwhile.Title);
        var lastChange = publishedMeanwhile.StatusChanges[^1];
        Assert.Equal(PostStatus.Published, lastChange.FromStatus);
        Assert.Equal(PostStatus.Draft, lastChange.ToStatus);
        Assert.Equal(Author.UserId, lastChange.ActorId);
    }

    [Fact]
    public async Task AuthorEdit_RetryWithoutConfirmation_ReportsConfirmationRequired()
    {
        var seenByAuthor = CreateReturned();
        var approvedMeanwhile = CreateApproved();
        var repository = new ConflictingPostRepository(seenByAuthor, approvedMeanwhile);
        var service = CreateAuthorService(repository);
        var versionBefore = approvedMeanwhile.Version;

        var result = await service.EditAsync(seenByAuthor.Id, "New title", "New body", confirmRevertToDraft: false);

        Assert.Equal(ErrorCode.ConfirmationRequired, result.Error?.Code);
        Assert.Equal(1, repository.SaveAttempts);
        Assert.Equal(0, repository.CommittedSaves);
        Assert.Equal(versionBefore, approvedMeanwhile.Version);
    }

    // FR-014, R-11
    [Fact]
    public async Task EditorPublish_AfterConcurrentAuthorEdit_IsStaleWithoutRetry()
    {
        var seenByEditor = CreateApproved();
        var editedMeanwhile = CreateApproved();
        editedMeanwhile.EditAndRevertToDraft(Author.UserId, Author.DisplayName, "Edited", "Edited", Start.AddMinutes(4));
        var repository = new ConflictingPostRepository(seenByEditor, editedMeanwhile);
        var service = CreateEditorService(repository);
        var versionBefore = editedMeanwhile.Version;
        var historyCountBefore = editedMeanwhile.StatusChanges.Count;

        var result = await service.PublishAsync(seenByEditor.Id, seenByEditor.Version);

        Assert.Equal(ErrorCode.StaleVersion, result.Error?.Code);
        Assert.Equal(nameof(PostStatus.Draft), result.Error!.CurrentStatus);
        Assert.Equal(versionBefore, result.Error.CurrentVersion);
        Assert.Equal(1, repository.SaveAttempts);
        Assert.Equal(0, repository.CommittedSaves);
        Assert.Equal(PostStatus.Draft, editedMeanwhile.Status);
        Assert.Equal(versionBefore, editedMeanwhile.Version);
        Assert.Equal(historyCountBefore, editedMeanwhile.StatusChanges.Count);
    }

    // U4, research.md R-11
    [Fact]
    public async Task AuthorEdit_ConflictingOnRetryToo_IsStaleAfterExactlyOneRetry()
    {
        var seenByAuthor = CreateApproved();
        var publishedMeanwhile = CreatePublished();
        var unpublishedMeanwhile = CreatePublished();
        unpublishedMeanwhile.Unpublish(Editor.UserId, Editor.DisplayName, Start.AddMinutes(5));
        var repository = new ConflictingPostRepository(seenByAuthor, publishedMeanwhile, unpublishedMeanwhile);
        var service = CreateAuthorService(repository);
        var versionBefore = unpublishedMeanwhile.Version;
        var historyCountBefore = unpublishedMeanwhile.StatusChanges.Count;

        var result = await service.EditAsync(seenByAuthor.Id, "New title", "New body", confirmRevertToDraft: true);

        Assert.Equal(ErrorCode.StaleVersion, result.Error?.Code);
        Assert.Equal(nameof(PostStatus.Approved), result.Error!.CurrentStatus);
        Assert.Equal(versionBefore, result.Error.CurrentVersion);
        Assert.Equal(2, repository.SaveAttempts);
        Assert.Equal(0, repository.CommittedSaves);
        Assert.Same(unpublishedMeanwhile, repository.Current);
        Assert.Equal(PostStatus.Approved, unpublishedMeanwhile.Status);
        Assert.Equal("Title", unpublishedMeanwhile.Title);
        Assert.Equal(versionBefore, unpublishedMeanwhile.Version);
        Assert.Equal(historyCountBefore, unpublishedMeanwhile.StatusChanges.Count);
    }

    private AuthorPostService CreateAuthorService(IPostRepository repository) =>
        new(new FixedCurrentUser(Author.UserId), new FixedUserAccessReader(Author), repository, _time);

    private EditorPostService CreateEditorService(IPostRepository repository) =>
        new(new FixedCurrentUser(Editor.UserId), new FixedUserAccessReader(Editor), repository, _time);

    private static Post CreateReturned()
    {
        var post = Post.Create(Author.UserId, Author.DisplayName, "Title", "Body", Start);
        post.Submit(Author.UserId, Author.DisplayName, Start.AddMinutes(1));
        post.Return(Editor.UserId, Editor.DisplayName, "Needs work", Start.AddMinutes(2));
        return post;
    }

    private static Post CreateApproved()
    {
        var post = Post.Create(Author.UserId, Author.DisplayName, "Title", "Body", Start);
        post.Submit(Author.UserId, Author.DisplayName, Start.AddMinutes(1));
        post.Approve(Editor.UserId, Editor.DisplayName, Start.AddMinutes(2));
        return post;
    }

    private static Post CreatePublished()
    {
        var post = CreateApproved();
        post.Publish(Editor.UserId, Editor.DisplayName, Start.AddMinutes(3));
        return post;
    }

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class FixedUserAccessReader(UserAccess access) : IUserAccessReader
    {
        public Task<UserAccess?> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserAccess?>(userId == access.UserId ? access : null);
    }

    /// <summary>
    /// Stores a single post. Each queued post is a change made by another user that becomes visible when the
    /// next save is attempted, so that save fails with <see cref="ConcurrencyConflictException"/> and nothing
    /// is stored. A save with no queued change is committed.
    /// </summary>
    private sealed class ConflictingPostRepository(Post initial, params Post[] concurrentChanges) : IPostRepository
    {
        private readonly Queue<Post> _concurrentChanges = new(concurrentChanges);

        public Post Current { get; private set; } = initial;

        public int SaveAttempts { get; private set; }

        public int CommittedSaves { get; private set; }

        public Task<Post?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Post?>(Current);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveAttempts++;

            if (_concurrentChanges.TryDequeue(out var changed))
            {
                Current = changed;
                throw new ConcurrencyConflictException();
            }

            CommittedSaves++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Post>> ListByAuthorAsync(Guid authorId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Post>> ListByStatusAsync(PostStatus status, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Post>> ListPublishedAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Add(Post post) => throw new NotSupportedException();

        public void Remove(Post post) => throw new NotSupportedException();
    }
}
