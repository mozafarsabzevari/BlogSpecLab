using System.Net;
using System.Text.Json;
using BlogSpecLab.Core.Application.ContentReviews;
using BlogSpecLab.Tests.Api.Infrastructure;
using static BlogSpecLab.Tests.Api.Infrastructure.ContentReviewApi;

namespace BlogSpecLab.Tests.Api;

/// <summary>Acceptance tests for user story 1 with the fake model (quickstart.md).</summary>
public class ContentReviewTests : IAsyncLifetime
{
    private readonly BlogSpecLabApiFactory _factory = new();
    private TestUsers _users = null!;

    private FakeContentReviewModel Model => _factory.ContentReviewModel;

    public async Task InitializeAsync()
    {
        _users = await TestUsers.CreateAsync(_factory);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // Scenario 1
    [Fact]
    public async Task Request_WithIssues_ReturnsIssuesOrderedBySeverity()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var (id, _) = await CreatePostAsync(authorA);
        Model.Returns(FakeContentReviewModel.Completed(
            FakeContentReviewModel.Issue("جملهٔ دوم", severity: "low", category: "structure_and_coherence"),
            FakeContentReviewModel.Issue("میخواهیم", severity: "high", category: "spelling_and_writing", suggestion: "می‌خواهیم"),
            FakeContentReviewModel.Issue("عنوان", location: "title", severity: "medium", category: "title_body_mismatch")));

        var review = await RequestReviewSucceedsAsync(authorA, id);

        Assert.Equal("IssuesFound", review.GetProperty("outcome").GetString());
        Assert.Equal(id, review.GetProperty("postId").GetGuid());
        var issues = review.GetProperty("issues").EnumerateArray().ToList();
        Assert.Equal(["High", "Medium", "Low"], issues.Select(issue => issue.GetProperty("severity").GetString()));

        var first = issues[0];
        Assert.Equal("SpellingAndWriting", first.GetProperty("category").GetString());
        Assert.Equal("Body", first.GetProperty("location").GetString());
        Assert.Equal("میخواهیم", first.GetProperty("excerpt").GetString());
        Assert.False(string.IsNullOrEmpty(first.GetProperty("explanation").GetString()));
        Assert.Equal("می‌خواهیم", first.GetProperty("suggestion").GetString());
        Assert.Equal("Title", issues[1].GetProperty("location").GetString());
        Assert.Empty(review.GetProperty("missingInformation").EnumerateArray());
    }

    // Scenario 1, contracts/http-api.md
    [Fact]
    public async Task Response_DoesNotExposeModelPromptOrSchemaVersions()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var (id, _) = await CreatePostAsync(authorA);

        var review = await RequestReviewSucceedsAsync(authorA, id);

        string[] expected =
            ["id", "postId", "outcome", "reviewedVersion", "reviewedPostModifiedAt", "isCurrentVersion", "createdAt", "issues", "missingInformation"];
        var names = review.EnumerateObject().Select(property => property.Name);
        Assert.Equal(expected.Order(), names.Order());
        Assert.DoesNotContain(FakeContentReviewModel.ModelName, AllText(review));
        Assert.DoesNotContain(FakeContentReviewModel.PromptVersion, AllText(review));
    }

    // Scenario 2
    [Fact]
    public async Task Request_WithoutIssues_IsNoIssuesFound()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var (id, _) = await CreatePostAsync(authorA);
        Model.Returns(FakeContentReviewModel.Completed());

        var review = await RequestReviewSucceedsAsync(authorA, id);

        Assert.Equal("NoIssuesFound", review.GetProperty("outcome").GetString());
        Assert.Empty(review.GetProperty("issues").EnumerateArray());
    }

    // Scenario 3
    [Fact]
    public async Task Request_ForReturnedPost_IsAllowed()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, etag) = await CreatePostAsync(authorA);
        etag = await SubmitAsync(authorA, id, etag);
        await EditorActionAsync(editor2, id, "return", etag, new { reason = "منبع اضافه کنید" });

        var review = await RequestReviewSucceedsAsync(authorA, id);

        Assert.Equal("NoIssuesFound", review.GetProperty("outcome").GetString());
    }

    // Scenario 4, FR-003
    [Fact]
    public async Task Response_ShowsReviewedVersionAndTurnsStaleAfterEdit()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var (id, _) = await CreatePostAsync(authorA);
        var post = await GetOwnPostAsync(authorA, id);
        _factory.Time.Advance(TimeSpan.FromMinutes(1));

        var review = await RequestReviewSucceedsAsync(authorA, id);

        Assert.Equal(post.GetProperty("version").GetGuid(), review.GetProperty("reviewedVersion").GetGuid());
        Assert.Equal(
            post.GetProperty("lastModifiedAt").GetDateTimeOffset(),
            review.GetProperty("reviewedPostModifiedAt").GetDateTimeOffset());
        Assert.Equal(_factory.Time.GetUtcNow(), review.GetProperty("createdAt").GetDateTimeOffset());
        Assert.True(review.GetProperty("isCurrentVersion").GetBoolean());

        await EditAsync(authorA, id, "عنوان ویرایش‌شده", "متن ویرایش‌شده");

        var stored = Assert.Single(await ListReviewsAsync(authorA, id));
        Assert.Equal(review.GetProperty("id").GetGuid(), stored.GetProperty("id").GetGuid());
        Assert.False(stored.GetProperty("isCurrentVersion").GetBoolean());
    }

    // Scenario 7, FR-008, SC-004
    [Fact]
    public async Task IssueWithoutRealReference_IsDropped()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var (id, _) = await CreatePostAsync(authorA);
        Model.Returns(FakeContentReviewModel.Completed(
            FakeContentReviewModel.Issue("جملهٔ دوم"),
            FakeContentReviewModel.Issue("عبارتی که در پست نیست")));

        var review = await RequestReviewSucceedsAsync(authorA, id);

        var issue = Assert.Single(review.GetProperty("issues").EnumerateArray());
        Assert.Equal("جملهٔ دوم", issue.GetProperty("excerpt").GetString());
    }

    // Scenario 8, R-07 rule 6
    [Fact]
    public async Task AllIssuesWithoutReference_IsOutputInvalidAndNothingStored()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var (id, _) = await CreatePostAsync(authorA);
        Model.Returns(FakeContentReviewModel.Completed(FakeContentReviewModel.Issue("ساختگی")));

        var response = await RequestReviewAsync(authorA, id);

        await AssertProblemWithoutContentAsync(response, HttpStatusCode.BadGateway, "ReviewOutputInvalid");
        Assert.Empty(await ListReviewsAsync(authorA, id));
    }

    // Scenario 11, FR-002, SC-005, SC-008
    [Fact]
    public async Task OtherAuthor_CannotRequestOrListReviews()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var authorB = _factory.CreateClientFor(_users.AuthorB);
        var (id, _) = await CreatePostAsync(authorA);
        await RequestReviewSucceedsAsync(authorA, id);

        await AssertProblemWithoutContentAsync(await RequestReviewAsync(authorB, id), HttpStatusCode.NotFound, "NotFound");
        await AssertProblemWithoutContentAsync(await authorB.GetAsync(ReviewsPath(id)), HttpStatusCode.NotFound, "NotFound");
        Assert.Single(Model.Inputs);
    }

    // Scenario 12, FR-013, SC-008
    [Fact]
    public async Task Editors_CannotSeeReviews()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var (id, _) = await CreatePostAsync(authorA);
        await RequestReviewSucceedsAsync(authorA, id);

        await AssertProblemWithoutContentAsync(
            await _factory.CreateClientFor(_users.Editor2).GetAsync(ReviewsPath(id)), HttpStatusCode.Forbidden, "Forbidden");
        await AssertProblemWithoutContentAsync(
            await _factory.CreateClientFor(_users.Editor1).GetAsync(ReviewsPath(id)), HttpStatusCode.NotFound, "NotFound");
    }

    // Scenario 13
    [Fact]
    public async Task Anonymous_IsUnauthenticated()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var (id, _) = await CreatePostAsync(authorA);
        var anonymous = _factory.CreateClient();

        await AssertProblemAsync(await RequestReviewAsync(anonymous, id), HttpStatusCode.Unauthorized, "Unauthenticated");
        await AssertProblemAsync(await anonymous.GetAsync(ReviewsPath(id)), HttpStatusCode.Unauthorized, "Unauthenticated");
        Assert.Empty(Model.Inputs);
    }

    // Scenario 14, FR-002
    [Fact]
    public async Task Request_AfterSubmissionApprovalOrPublication_IsNotAvailable()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, etag) = await CreatePostAsync(authorA);

        etag = await SubmitAsync(authorA, id, etag);
        await AssertNotAvailableAsync(authorA, id, "InReview");

        etag = await EditorActionAsync(editor2, id, "approve", etag);
        await AssertNotAvailableAsync(authorA, id, "Approved");

        await EditorActionAsync(editor2, id, "publish", etag);
        await AssertNotAvailableAsync(authorA, id, "Published");

        Assert.Empty(Model.Inputs);
    }

    // Scenario 16, FR-011, R-13
    [Fact]
    public async Task ProviderUnavailable_Is503AndOtherPostRoutesKeepWorking()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var (id, _) = await CreatePostAsync(authorA);
        Model.Returns(new ContentReviewModelOutcome.ProviderUnavailable());

        await AssertProblemWithoutContentAsync(
            await RequestReviewAsync(authorA, id), HttpStatusCode.ServiceUnavailable, "ReviewServiceUnavailable");

        await EditAsync(authorA, id, "عنوان تازه", "متن تازه");
        Assert.Empty(await ListReviewsAsync(authorA, id));
    }

    // Scenario 17, R-08
    [Fact]
    public async Task RefusedAndUnexpectedForm_AreBadGatewayWithDistinctCodes()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var (id, _) = await CreatePostAsync(authorA);

        Model.Returns(new ContentReviewModelOutcome.Refused());
        await AssertProblemWithoutContentAsync(await RequestReviewAsync(authorA, id), HttpStatusCode.BadGateway, "ReviewRefused");

        Model.Returns(new ContentReviewModelOutcome.UnexpectedOutputForm());
        await AssertProblemWithoutContentAsync(await RequestReviewAsync(authorA, id), HttpStatusCode.BadGateway, "ReviewOutputInvalid");

        Model.Returns(new ContentReviewModelOutcome.InvalidOutput());
        await AssertProblemWithoutContentAsync(await RequestReviewAsync(authorA, id), HttpStatusCode.BadGateway, "ReviewOutputInvalid");

        Assert.Empty(await ListReviewsAsync(authorA, id));
    }

    // Scenario 19, FR-013
    [Fact]
    public async Task ThreeReviews_AreListedNewestFirst()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var (id, _) = await CreatePostAsync(authorA);
        var ids = new List<Guid>();

        for (var i = 0; i < 3; i++)
        {
            _factory.Time.Advance(TimeSpan.FromMinutes(1));
            ids.Add((await RequestReviewSucceedsAsync(authorA, id)).GetProperty("id").GetGuid());
        }

        var listed = await ListReviewsAsync(authorA, id);

        Assert.Equal(Enumerable.Reverse(ids), listed.Select(review => review.GetProperty("id").GetGuid()));
        Assert.All(listed, review => Assert.True(review.GetProperty("isCurrentVersion").GetBoolean()));
    }

    // Scenario 21, R-11
    [Fact]
    public async Task PostDeletedDuringReview_IsNotFoundAndNothingStored()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var (id, etag) = await CreatePostAsync(authorA);
        Model.Hold();

        var pending = RequestReviewAsync(authorA, id);
        await Model.WhenCalled;
        var deleted = await SendAsync(authorA, HttpMethod.Delete, $"{PostsPath}/{id}", etag);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Model.Release();

        await AssertProblemWithoutContentAsync(await pending, HttpStatusCode.NotFound, "NotFound");
    }

    [Fact]
    public async Task UnknownPost_IsNotFound()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);

        await AssertProblemAsync(await RequestReviewAsync(authorA, Guid.NewGuid()), HttpStatusCode.NotFound, "NotFound");
        await AssertProblemAsync(await authorA.GetAsync(ReviewsPath(Guid.NewGuid())), HttpStatusCode.NotFound, "NotFound");
    }

    private static async Task AssertNotAvailableAsync(HttpClient author, Guid id, string expectedStatus)
    {
        var problem = await AssertProblemWithoutContentAsync(
            await RequestReviewAsync(author, id), HttpStatusCode.Conflict, "ReviewNotAvailable");
        Assert.Equal(expectedStatus, problem.GetProperty("currentStatus").GetString());
    }

    /// <summary>Error responses never carry post content or model output (contracts/http-api.md).</summary>
    private static async Task<JsonElement> AssertProblemWithoutContentAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        var problem = await AssertProblemAsync(response, expectedStatus, expectedCode);
        var text = AllText(problem);
        Assert.DoesNotContain(DefaultTitle, text);
        Assert.DoesNotContain("میخواهیم", text);
        Assert.DoesNotContain("توضیح مشکل", text);
        return problem;
    }
}
