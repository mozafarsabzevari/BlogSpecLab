using BlogSpecLab.Core.Domain.Posts;

namespace BlogSpecLab.Tests.Domain;

public class PostDraftTests
{
    private static readonly Guid AuthorId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private static Post CreateDraft(string title = "Title", string body = "Body") =>
        Post.Create(AuthorId, "Author A", title, body, Now);

    [Fact]
    public void Create_StartsAsDraftWithInitialHistoryAndVersion()
    {
        var post = CreateDraft();

        Assert.NotEqual(Guid.Empty, post.Id);
        Assert.Equal(PostStatus.Draft, post.Status);
        Assert.Equal(AuthorId, post.AuthorId);
        Assert.Equal("Author A", post.AuthorDisplayName);
        Assert.Equal(Now, post.CreatedAt);
        Assert.Equal(Now, post.LastModifiedAt);
        Assert.Null(post.LastPublishedAt);
        Assert.False(post.HasEverBeenPublished);
        Assert.NotEqual(Guid.Empty, post.Version);

        var change = Assert.Single(post.StatusChanges);
        Assert.Equal(post.Id, change.PostId);
        Assert.Null(change.FromStatus);
        Assert.Equal(PostStatus.Draft, change.ToStatus);
        Assert.Equal(AuthorId, change.ActorId);
        Assert.Equal("Author A", change.ActorDisplayName);
        Assert.Equal(Now, change.OccurredAt);
        Assert.Null(change.Reason);
    }

    [Fact]
    public void Create_GivesEachPostItsOwnVersion()
    {
        Assert.NotEqual(CreateDraft().Version, CreateDraft().Version);
    }

    [Fact]
    public void Create_TrimsTitleAndBody()
    {
        var post = CreateDraft("  Title  ", "\n Body \t");

        Assert.Equal("Title", post.Title);
        Assert.Equal("Body", post.Body);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsMissingTitle(string? title)
    {
        var error = Assert.Throws<PostDomainException>(() => Post.Create(AuthorId, "Author A", title, "Body", Now));

        Assert.Equal(PostErrorCode.InvalidTitle, error.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsMissingBody(string? body)
    {
        var error = Assert.Throws<PostDomainException>(() => Post.Create(AuthorId, "Author A", "Title", body, Now));

        Assert.Equal(PostErrorCode.InvalidBody, error.Code);
    }

    [Fact]
    public void Create_AcceptsTitleOf200CharactersAfterTrim()
    {
        var title = new string('ع', 200);

        var post = CreateDraft($"  {title}  ");

        Assert.Equal(title, post.Title);
    }

    [Fact]
    public void Create_RejectsTitleOf201Characters()
    {
        var error = Assert.Throws<PostDomainException>(() => CreateDraft(title: new string('ع', 201)));

        Assert.Equal(PostErrorCode.InvalidTitle, error.Code);
    }

    [Fact]
    public void Create_AcceptsBodyOf50000CharactersAfterTrim()
    {
        var body = new string('م', 50_000);

        var post = CreateDraft(body: $" {body} ");

        Assert.Equal(body, post.Body);
    }

    [Fact]
    public void Create_RejectsBodyOf50001Characters()
    {
        var error = Assert.Throws<PostDomainException>(() => CreateDraft(body: new string('م', 50_001)));

        Assert.Equal(PostErrorCode.InvalidBody, error.Code);
    }

    [Fact]
    public void Edit_InDraft_UpdatesContentTimeAndVersionWithoutStatusChange()
    {
        var post = CreateDraft();
        var originalVersion = post.Version;
        var later = Now.AddMinutes(5);

        post.Edit("  New title ", " New body ", later);

        Assert.Equal("New title", post.Title);
        Assert.Equal("New body", post.Body);
        Assert.Equal(PostStatus.Draft, post.Status);
        Assert.Equal(later, post.LastModifiedAt);
        Assert.Equal(Now, post.CreatedAt);
        Assert.NotEqual(originalVersion, post.Version);
        Assert.Single(post.StatusChanges);
    }

    [Fact]
    public void Edit_EachEditProducesANewVersion()
    {
        var post = CreateDraft();

        post.Edit("Second", "Body", Now.AddMinutes(1));
        var secondVersion = post.Version;
        post.Edit("Third", "Body", Now.AddMinutes(2));

        Assert.NotEqual(secondVersion, post.Version);
    }

    [Fact]
    public void Edit_WithInvalidContent_IsRejectedAndLeavesPostUnchanged()
    {
        var post = CreateDraft();
        var originalVersion = post.Version;

        var error = Assert.Throws<PostDomainException>(() => post.Edit("Title", " ", Now.AddMinutes(1)));

        Assert.Equal(PostErrorCode.InvalidBody, error.Code);
        Assert.Equal("Body", post.Body);
        Assert.Equal(originalVersion, post.Version);
        Assert.Equal(Now, post.LastModifiedAt);
    }

    [Fact]
    public void EnsureCanDelete_AllowsNeverPublishedDraft()
    {
        var post = CreateDraft();

        var exception = Record.Exception(post.EnsureCanDelete);

        Assert.Null(exception);
    }
}
