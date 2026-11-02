using System.Reflection;
using BlogSpecLab.Core.Domain.Posts;

namespace BlogSpecLab.Tests.Domain;

public class PostReviewTests
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

    [Fact]
    public void Submit_FromDraft_MovesToInReviewAndRecordsActor()
    {
        var post = CreateDraft();
        var originalVersion = post.Version;
        var later = Now.AddMinutes(1);

        post.Submit(AuthorId, "Author A", later);

        Assert.Equal(PostStatus.InReview, post.Status);
        Assert.Equal(later, post.LastModifiedAt);
        Assert.NotEqual(originalVersion, post.Version);
        AssertLastChange(post, AuthorId, "Author A", later, PostStatus.Draft, PostStatus.InReview, reason: null);
    }

    [Fact]
    public void Submit_FromReturned_MovesToInReview()
    {
        var post = CreateReturned();

        post.Submit(AuthorId, "Author A", Now.AddMinutes(3));

        Assert.Equal(PostStatus.InReview, post.Status);
        AssertLastChange(post, AuthorId, "Author A", Now.AddMinutes(3), PostStatus.Returned, PostStatus.InReview, null);
    }

    [Theory]
    [InlineData("Title", "")]
    [InlineData("", "Body")]
    public void Submit_WithInvalidStoredContent_IsRejectedAndLeavesPostUnchanged(string title, string body)
    {
        var post = CreateDraft();
        // Content can only become invalid through stored data, which the public API never produces.
        SetPrivate(post, nameof(Post.Title), title);
        SetPrivate(post, nameof(Post.Body), body);
        var originalVersion = post.Version;

        var error = Assert.Throws<PostDomainException>(() => post.Submit(AuthorId, "Author A", Now.AddMinutes(1)));

        Assert.Contains(error.Code, new[] { PostErrorCode.InvalidTitle, PostErrorCode.InvalidBody });
        Assert.Equal(PostStatus.Draft, post.Status);
        Assert.Equal(originalVersion, post.Version);
        Assert.Single(post.StatusChanges);
    }

    [Fact]
    public void Withdraw_FromInReview_MovesBackToDraft()
    {
        var post = CreateInReview();
        var originalVersion = post.Version;

        post.Withdraw(AuthorId, "Author A", Now.AddMinutes(2));

        Assert.Equal(PostStatus.Draft, post.Status);
        Assert.NotEqual(originalVersion, post.Version);
        AssertLastChange(post, AuthorId, "Author A", Now.AddMinutes(2), PostStatus.InReview, PostStatus.Draft, null);
    }

    [Fact]
    public void Approve_FromInReview_MovesToApprovedAndRecordsEditor()
    {
        var post = CreateInReview();
        var originalVersion = post.Version;

        post.Approve(EditorId, "Editor 2", Now.AddMinutes(2));

        Assert.Equal(PostStatus.Approved, post.Status);
        Assert.Equal(Now.AddMinutes(2), post.LastModifiedAt);
        Assert.NotEqual(originalVersion, post.Version);
        AssertLastChange(post, EditorId, "Editor 2", Now.AddMinutes(2), PostStatus.InReview, PostStatus.Approved, null);
    }

    [Fact]
    public void Approve_ByPostAuthor_IsRejectedAndLeavesPostUnchanged()
    {
        var post = CreateInReview();
        var originalVersion = post.Version;

        var error = Assert.Throws<PostDomainException>(() => post.Approve(AuthorId, "Author A", Now.AddMinutes(2)));

        Assert.Equal(PostErrorCode.SelfApprovalNotAllowed, error.Code);
        Assert.Equal(PostStatus.InReview, post.Status);
        Assert.Equal(originalVersion, post.Version);
        Assert.Equal(2, post.StatusChanges.Count);
    }

    [Fact]
    public void Return_FromInReview_MovesToReturnedWithTrimmedReason()
    {
        var post = CreateInReview();
        var originalVersion = post.Version;

        post.Return(EditorId, "Editor 2", "  Add sources  ", Now.AddMinutes(2));

        Assert.Equal(PostStatus.Returned, post.Status);
        Assert.Equal("Add sources", post.LastReturnReason);
        Assert.NotEqual(originalVersion, post.Version);
        AssertLastChange(
            post, EditorId, "Editor 2", Now.AddMinutes(2), PostStatus.InReview, PostStatus.Returned, "Add sources");
    }

    [Fact]
    public void Return_ByEditorWhoIsAlsoTheAuthor_IsAllowed()
    {
        var post = CreateInReview();

        post.Return(AuthorId, "Author A", "Rework", Now.AddMinutes(2));

        Assert.Equal(PostStatus.Returned, post.Status);
    }

    [Fact]
    public void Return_ReplacesPreviousReturnReason()
    {
        var post = CreateReturned();
        post.Submit(AuthorId, "Author A", Now.AddMinutes(3));

        post.Return(EditorId, "Editor 2", "Second reason", Now.AddMinutes(4));

        Assert.Equal("Second reason", post.LastReturnReason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Return_WithoutReason_IsRejectedAndLeavesPostUnchanged(string? reason)
    {
        var post = CreateInReview();
        var originalVersion = post.Version;

        var error = Assert.Throws<PostDomainException>(() => post.Return(EditorId, "Editor 2", reason, Now.AddMinutes(2)));

        Assert.Equal(PostErrorCode.ReturnReasonRequired, error.Code);
        Assert.Equal(PostStatus.InReview, post.Status);
        Assert.Null(post.LastReturnReason);
        Assert.Equal(originalVersion, post.Version);
    }

    public static TheoryData<string> InvalidTransitions =>
    [
        "Submit from InReview",
        "Submit from Approved",
        "Withdraw from Draft",
        "Withdraw from Returned",
        "Withdraw from Approved",
        "Approve from Draft",
        "Approve from Returned",
        "Approve from Approved",
        "Return from Draft",
        "Return from Returned",
        "Return from Approved"
    ];

    [Theory]
    [MemberData(nameof(InvalidTransitions))]
    public void InvalidTransition_IsRejectedAndLeavesPostUnchanged(string transition)
    {
        var parts = transition.Split(" from ");
        var post = parts[1] switch
        {
            "Draft" => CreateDraft(),
            "InReview" => CreateInReview(),
            "Returned" => CreateReturned(),
            "Approved" => CreateApproved(),
            _ => throw new ArgumentOutOfRangeException(nameof(transition))
        };
        var originalStatus = post.Status;
        var originalVersion = post.Version;
        var originalHistoryCount = post.StatusChanges.Count;
        var later = Now.AddMinutes(10);

        Action action = parts[0] switch
        {
            "Submit" => () => post.Submit(AuthorId, "Author A", later),
            "Withdraw" => () => post.Withdraw(AuthorId, "Author A", later),
            "Approve" => () => post.Approve(EditorId, "Editor 2", later),
            "Return" => () => post.Return(EditorId, "Editor 2", "Reason", later),
            _ => throw new ArgumentOutOfRangeException(nameof(transition))
        };

        var error = Assert.Throws<PostDomainException>(action);

        Assert.Equal(PostErrorCode.InvalidStatusTransition, error.Code);
        Assert.Equal(originalStatus, post.Status);
        Assert.Equal(originalVersion, post.Version);
        Assert.Equal(originalHistoryCount, post.StatusChanges.Count);
    }

    [Fact]
    public void Edit_InReturned_UpdatesContentAndVersionWithoutStatusChange()
    {
        var post = CreateReturned();
        var originalVersion = post.Version;
        var historyCount = post.StatusChanges.Count;

        post.Edit("Fixed title", "Fixed body", Now.AddMinutes(3));

        Assert.Equal("Fixed title", post.Title);
        Assert.Equal("Fixed body", post.Body);
        Assert.Equal(PostStatus.Returned, post.Status);
        Assert.Equal(Now.AddMinutes(3), post.LastModifiedAt);
        Assert.NotEqual(originalVersion, post.Version);
        Assert.Equal(historyCount, post.StatusChanges.Count);
    }

    [Fact]
    public void Edit_InReview_IsRejected()
    {
        var post = CreateInReview();
        var originalVersion = post.Version;

        var error = Assert.Throws<PostDomainException>(() => post.Edit("New", "New", Now.AddMinutes(2)));

        Assert.Equal(PostErrorCode.InvalidStatusTransition, error.Code);
        Assert.Equal("Title", post.Title);
        Assert.Equal(originalVersion, post.Version);
    }

    [Fact]
    public void EnsureCanDelete_AllowsNeverPublishedReturnedPost()
    {
        var post = CreateReturned();

        var exception = Record.Exception(post.EnsureCanDelete);

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureCanDelete_RejectsPostInReview()
    {
        var post = CreateInReview();

        var error = Assert.Throws<PostDomainException>(post.EnsureCanDelete);

        Assert.Equal(PostErrorCode.InvalidStatusTransition, error.Code);
    }

    private static void AssertLastChange(
        Post post,
        Guid actorId,
        string actorDisplayName,
        DateTimeOffset occurredAt,
        PostStatus fromStatus,
        PostStatus toStatus,
        string? reason)
    {
        var change = post.StatusChanges[^1];
        Assert.Equal(post.Id, change.PostId);
        Assert.Equal(actorId, change.ActorId);
        Assert.Equal(actorDisplayName, change.ActorDisplayName);
        Assert.Equal(occurredAt, change.OccurredAt);
        Assert.Equal(fromStatus, change.FromStatus);
        Assert.Equal(toStatus, change.ToStatus);
        Assert.Equal(reason, change.Reason);
    }

    private static void SetPrivate(Post post, string propertyName, string value) =>
        typeof(Post).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(post, [value]);
}
