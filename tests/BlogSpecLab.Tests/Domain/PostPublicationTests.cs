using BlogSpecLab.Core.Domain.Posts;

namespace BlogSpecLab.Tests.Domain;

public class PostPublicationTests
{
    private static readonly Guid AuthorId = Guid.NewGuid();
    private static readonly Guid EditorId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private static Post CreateDraft() => Post.Create(AuthorId, "Author A", "Title", "Body", Now);

    private static Post CreateInReview()
    {
        var post = CreateDraft();
        post.Submit(AuthorId, "Author A", Now.AddMinutes(1));
        return post;
    }

    private static Post CreateReturned()
    {
        var post = CreateInReview();
        post.Return(EditorId, "Editor 2", "Needs work", Now.AddMinutes(2));
        return post;
    }

    private static Post CreateApproved()
    {
        var post = CreateInReview();
        post.Approve(EditorId, "Editor 2", Now.AddMinutes(2));
        return post;
    }

    private static Post CreatePublished()
    {
        var post = CreateApproved();
        post.Publish(EditorId, "Editor 2", Now.AddMinutes(3));
        return post;
    }

    [Fact]
    public void Publish_FromApproved_MovesToPublishedAndSetsLastPublishedAt()
    {
        var post = CreateApproved();
        var originalVersion = post.Version;
        var later = Now.AddMinutes(3);

        post.Publish(EditorId, "Editor 2", later);

        Assert.Equal(PostStatus.Published, post.Status);
        Assert.Equal(later, post.LastPublishedAt);
        Assert.True(post.HasEverBeenPublished);
        Assert.Equal(later, post.LastModifiedAt);
        Assert.NotEqual(originalVersion, post.Version);
        AssertLastChange(post, EditorId, "Editor 2", later, PostStatus.Approved, PostStatus.Published);
    }

    [Fact]
    public void Publish_ByEditorWhoIsAlsoTheAuthor_IsAllowed()
    {
        var post = CreateApproved();

        post.Publish(AuthorId, "Author A", Now.AddMinutes(3));

        Assert.Equal(PostStatus.Published, post.Status);
    }

    [Fact]
    public void Unpublish_FromPublished_MovesToApprovedAndKeepsLastPublishedAt()
    {
        var post = CreatePublished();
        var publishedAt = post.LastPublishedAt;
        var originalVersion = post.Version;

        post.Unpublish(EditorId, "Editor 2", Now.AddMinutes(4));

        Assert.Equal(PostStatus.Approved, post.Status);
        Assert.Equal(publishedAt, post.LastPublishedAt);
        Assert.NotEqual(originalVersion, post.Version);
        AssertLastChange(post, EditorId, "Editor 2", Now.AddMinutes(4), PostStatus.Published, PostStatus.Approved);
    }

    [Fact]
    public void Republish_AfterUnpublish_UpdatesLastPublishedAtWithoutNewApproval()
    {
        var post = CreatePublished();
        post.Unpublish(EditorId, "Editor 2", Now.AddMinutes(4));

        post.Publish(EditorId, "Editor 2", Now.AddMinutes(5));

        Assert.Equal(PostStatus.Published, post.Status);
        Assert.Equal(Now.AddMinutes(5), post.LastPublishedAt);
    }

    public static TheoryData<string> InvalidTransitions =>
    [
        "Publish from Draft",
        "Publish from InReview",
        "Publish from Returned",
        "Publish from Published",
        "Unpublish from Draft",
        "Unpublish from InReview",
        "Unpublish from Returned",
        "Unpublish from Approved"
    ];

    [Theory]
    [MemberData(nameof(InvalidTransitions))]
    public void InvalidTransition_IsRejectedAndLeavesPostUnchanged(string transition)
    {
        var parts = transition.Split(" from ");
        var post = CreateIn(parts[1]);
        var originalStatus = post.Status;
        var originalVersion = post.Version;
        var originalPublishedAt = post.LastPublishedAt;
        var originalHistoryCount = post.StatusChanges.Count;
        var later = Now.AddMinutes(10);

        Action action = parts[0] switch
        {
            "Publish" => () => post.Publish(EditorId, "Editor 2", later),
            "Unpublish" => () => post.Unpublish(EditorId, "Editor 2", later),
            _ => throw new ArgumentOutOfRangeException(nameof(transition))
        };

        var error = Assert.Throws<PostDomainException>(action);

        Assert.Equal(PostErrorCode.InvalidStatusTransition, error.Code);
        Assert.Equal(originalStatus, post.Status);
        Assert.Equal(originalVersion, post.Version);
        Assert.Equal(originalPublishedAt, post.LastPublishedAt);
        Assert.Equal(originalHistoryCount, post.StatusChanges.Count);
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Published")]
    public void EditAndRevertToDraft_FromApprovedOrPublished_UpdatesContentAndMovesToDraft(string status)
    {
        var post = CreateIn(status);
        var originalStatus = post.Status;
        var originalPublishedAt = post.LastPublishedAt;
        var originalVersion = post.Version;
        var later = Now.AddMinutes(10);

        post.EditAndRevertToDraft(AuthorId, "Author A", " New title ", " New body ", later);

        Assert.Equal(PostStatus.Draft, post.Status);
        Assert.Equal("New title", post.Title);
        Assert.Equal("New body", post.Body);
        Assert.Equal(originalPublishedAt, post.LastPublishedAt);
        Assert.Equal(later, post.LastModifiedAt);
        Assert.NotEqual(originalVersion, post.Version);
        AssertLastChange(post, AuthorId, "Author A", later, originalStatus, PostStatus.Draft);
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("InReview")]
    [InlineData("Returned")]
    public void EditAndRevertToDraft_FromOtherStatuses_IsRejected(string status)
    {
        var post = CreateIn(status);
        var originalVersion = post.Version;
        var originalHistoryCount = post.StatusChanges.Count;

        var error = Assert.Throws<PostDomainException>(
            () => post.EditAndRevertToDraft(AuthorId, "Author A", "New", "New", Now.AddMinutes(10)));

        Assert.Equal(PostErrorCode.InvalidStatusTransition, error.Code);
        Assert.Equal("Title", post.Title);
        Assert.Equal(originalVersion, post.Version);
        Assert.Equal(originalHistoryCount, post.StatusChanges.Count);
    }

    [Fact]
    public void EditAndRevertToDraft_WithInvalidContent_IsRejectedAndLeavesPostUnchanged()
    {
        var post = CreatePublished();
        var originalVersion = post.Version;

        var error = Assert.Throws<PostDomainException>(
            () => post.EditAndRevertToDraft(AuthorId, "Author A", " ", "Body", Now.AddMinutes(10)));

        Assert.Equal(PostErrorCode.InvalidTitle, error.Code);
        Assert.Equal(PostStatus.Published, post.Status);
        Assert.Equal(originalVersion, post.Version);
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Published")]
    public void Edit_WithoutRevertToDraft_IsRejectedForApprovedOrPublished(string status)
    {
        var post = CreateIn(status);
        var originalVersion = post.Version;

        var error = Assert.Throws<PostDomainException>(() => post.Edit("New", "New", Now.AddMinutes(10)));

        Assert.Equal(PostErrorCode.InvalidStatusTransition, error.Code);
        Assert.Equal(originalVersion, post.Version);
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Published")]
    [InlineData("Unpublished")]
    public void EnsureCanDelete_RejectsApprovedOrPublishedWithInvalidStatusTransition(string status)
    {
        var post = CreateIn(status);

        var error = Assert.Throws<PostDomainException>(post.EnsureCanDelete);

        Assert.Equal(PostErrorCode.InvalidStatusTransition, error.Code);
    }

    [Fact]
    public void EnsureCanDelete_RejectsPreviouslyPublishedDraftWithDeleteNotAllowed()
    {
        var post = CreatePublished();
        post.EditAndRevertToDraft(AuthorId, "Author A", "New", "New", Now.AddMinutes(10));

        var error = Assert.Throws<PostDomainException>(post.EnsureCanDelete);

        Assert.Equal(PostErrorCode.DeleteNotAllowed, error.Code);
    }

    /// <summary>"Unpublished" is an Approved post that has been published before.</summary>
    private static Post CreateIn(string status)
    {
        switch (status)
        {
            case "Draft":
                return CreateDraft();
            case "InReview":
                return CreateInReview();
            case "Returned":
                return CreateReturned();
            case "Approved":
                return CreateApproved();
            case "Published":
                return CreatePublished();
            case "Unpublished":
                var post = CreatePublished();
                post.Unpublish(EditorId, "Editor 2", Now.AddMinutes(4));
                return post;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }
    }

    private static void AssertLastChange(
        Post post,
        Guid actorId,
        string actorDisplayName,
        DateTimeOffset occurredAt,
        PostStatus fromStatus,
        PostStatus toStatus)
    {
        var change = post.StatusChanges[^1];
        Assert.Equal(post.Id, change.PostId);
        Assert.Equal(actorId, change.ActorId);
        Assert.Equal(actorDisplayName, change.ActorDisplayName);
        Assert.Equal(occurredAt, change.OccurredAt);
        Assert.Equal(fromStatus, change.FromStatus);
        Assert.Equal(toStatus, change.ToStatus);
        Assert.Null(change.Reason);
    }
}
