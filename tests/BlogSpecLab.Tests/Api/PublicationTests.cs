using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BlogSpecLab.Tests.Api.Infrastructure;

namespace BlogSpecLab.Tests.Api;

public class PublicationTests : IAsyncLifetime
{
    private const string AuthorPath = "/api/author/posts";
    private const string EditorPath = "/api/editor/posts";
    private const string PublicPath = "/api/posts";

    private readonly BlogSpecLabApiFactory _factory = new();
    private TestUsers _users = null!;

    public async Task InitializeAsync()
    {
        _users = await TestUsers.CreateAsync(_factory);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // Scenario 8
    [Fact]
    public async Task Editor_PublishesOwnPostApprovedByAnotherEditor()
    {
        var editor1 = _factory.CreateClientFor(_users.Editor1);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, etag) = await CreateApprovedPostAsync(editor1, editor2);

        var published = await SendAsync(editor1, HttpMethod.Post, $"{EditorPath}/{id}/publish", etag);

        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var post = await ReadJsonAsync(published);
        Assert.Equal("Published", post.GetProperty("status").GetString());
        Assert.Equal(_factory.Time.GetUtcNow(), post.GetProperty("lastPublishedAt").GetDateTimeOffset());
        Assert.Equal($"\"{post.GetProperty("version").GetGuid()}\"", published.Headers.ETag?.Tag);
        var lastChange = post.GetProperty("history").EnumerateArray().Last();
        Assert.Equal(_users.Editor1.Id, lastChange.GetProperty("actorId").GetGuid());
        Assert.Equal("Approved", lastChange.GetProperty("fromStatus").GetString());
        Assert.Equal("Published", lastChange.GetProperty("toStatus").GetString());
    }

    // Scenario 9 (publish part)
    [Fact]
    public async Task AuthorWithoutEditorRole_CannotPublish()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var authorB = _factory.CreateClientFor(_users.AuthorB);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, etag) = await CreateApprovedPostAsync(authorA, editor2);

        var response = await SendAsync(authorB, HttpMethod.Post, $"{EditorPath}/{id}/publish", etag);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "Forbidden");
        Assert.Equal("Approved", await GetAuthorStatusAsync(authorA, id));
    }

    // Scenario 10
    [Fact]
    public async Task Publish_PostInReview_IsRejected()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, etag) = await CreateSubmittedPostAsync(authorA, "Secret title", "Secret body");

        var response = await SendAsync(editor2, HttpMethod.Post, $"{EditorPath}/{id}/publish", etag);

        var problem = await AssertProblemAsync(response, HttpStatusCode.Conflict, "InvalidStatusTransition");
        Assert.Equal("InReview", problem.GetProperty("currentStatus").GetString());
        Assert.DoesNotContain("Secret", problem.GetRawText());
        Assert.Equal("InReview", await GetAuthorStatusAsync(authorA, id));
    }

    [Fact]
    public async Task Publish_DraftPost_IsHiddenFromEditor()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, etag) = await CreatePostAsync(authorA);

        var response = await SendAsync(editor2, HttpMethod.Post, $"{EditorPath}/{id}/publish", etag);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "NotFound");
        Assert.Equal("Draft", await GetAuthorStatusAsync(authorA, id));
    }

    // Scenario 12
    [Fact]
    public async Task UnpublishAndRepublish_WithoutNewApproval()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var anonymous = _factory.CreateClient();
        var (id, etag) = await CreatePublishedPostAsync(authorA, editor2);

        var unpublished = await SendAsync(editor2, HttpMethod.Post, $"{EditorPath}/{id}/unpublish", etag);

        Assert.Equal(HttpStatusCode.OK, unpublished.StatusCode);
        Assert.Equal("Approved", (await ReadJsonAsync(unpublished)).GetProperty("status").GetString());
        Assert.Empty((await ReadJsonAsync(await anonymous.GetAsync(PublicPath))).EnumerateArray());

        var republished = await SendAsync(editor2, HttpMethod.Post, $"{EditorPath}/{id}/publish", unpublished.Headers.ETag?.Tag);

        Assert.Equal(HttpStatusCode.OK, republished.StatusCode);
        Assert.Equal("Published", (await ReadJsonAsync(republished)).GetProperty("status").GetString());
        var item = Assert.Single((await ReadJsonAsync(await anonymous.GetAsync(PublicPath))).EnumerateArray());
        Assert.Equal(id, item.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Unpublish_ApprovedPost_IsRejected()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, etag) = await CreateApprovedPostAsync(authorA, editor2);

        var response = await SendAsync(editor2, HttpMethod.Post, $"{EditorPath}/{id}/unpublish", etag);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "InvalidStatusTransition");
        Assert.Equal("Approved", await GetAuthorStatusAsync(authorA, id));
    }

    // Scenario 13
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Edit_PublishedPostWithoutConfirmation_IsRejectedWithoutChange(bool sendConfirmationFalse)
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, etag) = await CreatePublishedPostAsync(authorA, editor2, "Secret title", "Secret body");
        object body = sendConfirmationFalse
            ? new { title = "Changed", body = "Changed", confirmRevertToDraft = false }
            : new { title = "Changed", body = "Changed" };

        var response = await authorA.PutAsJsonAsync($"{AuthorPath}/{id}", body);

        var problem = await AssertProblemAsync(response, HttpStatusCode.Conflict, "ConfirmationRequired");
        Assert.Equal("Published", problem.GetProperty("currentStatus").GetString());
        Assert.DoesNotContain("Secret", problem.GetRawText());
        var post = await ReadJsonAsync(await authorA.GetAsync($"{AuthorPath}/{id}"));
        Assert.Equal("Published", post.GetProperty("status").GetString());
        Assert.Equal("Secret title", post.GetProperty("title").GetString());
        Assert.Equal($"\"{post.GetProperty("version").GetGuid()}\"", etag);
    }

    [Fact]
    public async Task Edit_ApprovedPostWithoutConfirmation_IsRejected()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, _) = await CreateApprovedPostAsync(authorA, editor2);

        var response = await authorA.PutAsJsonAsync($"{AuthorPath}/{id}", new { title = "Changed", body = "Changed" });

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "ConfirmationRequired");
        Assert.Equal("Approved", await GetAuthorStatusAsync(authorA, id));
    }

    // Scenario 14
    [Fact]
    public async Task Edit_PublishedPostWithConfirmation_RevertsToDraftAndLeavesPublicList()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var anonymous = _factory.CreateClient();
        var (id, _) = await CreatePublishedPostAsync(authorA, editor2);
        Assert.Single((await ReadJsonAsync(await anonymous.GetAsync(PublicPath))).EnumerateArray());

        var response = await EditWithConfirmationAsync(authorA, id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var post = await ReadJsonAsync(response);
        Assert.Equal("Draft", post.GetProperty("status").GetString());
        Assert.Equal("Changed title", post.GetProperty("title").GetString());
        Assert.Empty((await ReadJsonAsync(await anonymous.GetAsync(PublicPath))).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"{PublicPath}/{id}")).StatusCode);
    }

    // Scenario 15
    [Fact]
    public async Task Delete_PreviouslyPublishedDraft_IsRejectedAndKeepsHistory()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, _) = await CreatePublishedPostAsync(authorA, editor2);
        var edited = await EditWithConfirmationAsync(authorA, id);

        var response = await SendAsync(authorA, HttpMethod.Delete, $"{AuthorPath}/{id}", edited.Headers.ETag?.Tag);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "DeleteNotAllowed");
        Assert.Equal("Draft", await GetAuthorStatusAsync(authorA, id));
        var history = await ReadJsonAsync(await editor2.GetAsync($"{EditorPath}/{id}/history"));
        Assert.Contains(
            history.GetProperty("history").EnumerateArray(),
            change => change.GetProperty("toStatus").GetString() == "Published");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_ApprovedOrPublishedPost_IsRejected(bool published)
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, etag) = published
            ? await CreatePublishedPostAsync(authorA, editor2)
            : await CreateApprovedPostAsync(authorA, editor2);
        var expectedStatus = published ? "Published" : "Approved";

        var response = await SendAsync(authorA, HttpMethod.Delete, $"{AuthorPath}/{id}", etag);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "InvalidStatusTransition");
        Assert.Equal(expectedStatus, await GetAuthorStatusAsync(authorA, id));
    }

    // Scenario 17, publish first
    [Fact]
    public async Task PublishThenEditWithConfirmation_EndsAsDraftAndEditorSeesWhy()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var anonymous = _factory.CreateClient();
        var (id, _) = await CreatePublishedPostAsync(authorA, editor2);

        var edited = await EditWithConfirmationAsync(authorA, id);

        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal("Draft", await GetAuthorStatusAsync(authorA, id));
        Assert.Empty((await ReadJsonAsync(await anonymous.GetAsync(PublicPath))).EnumerateArray());
        Assert.Empty((await ReadJsonAsync(await editor2.GetAsync($"{EditorPath}?status=Published"))).EnumerateArray());
        await AssertProblemAsync(await editor2.GetAsync($"{EditorPath}/{id}"), HttpStatusCode.NotFound, "NotFound");

        var lastChange = (await ReadJsonAsync(await editor2.GetAsync($"{EditorPath}/{id}/history")))
            .GetProperty("history").EnumerateArray().Last();
        Assert.Equal("Published", lastChange.GetProperty("fromStatus").GetString());
        Assert.Equal("Draft", lastChange.GetProperty("toStatus").GetString());
        Assert.Equal(_users.AuthorA.Id, lastChange.GetProperty("actorId").GetGuid());
        Assert.Equal("Author A", lastChange.GetProperty("actorDisplayName").GetString());
    }

    // Scenario 17, edit first
    [Fact]
    public async Task EditWithConfirmationThenPublish_EndsAsDraft()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var anonymous = _factory.CreateClient();
        var (id, approvedETag) = await CreateApprovedPostAsync(authorA, editor2);

        var edited = await EditWithConfirmationAsync(authorA, id);
        var published = await SendAsync(editor2, HttpMethod.Post, $"{EditorPath}/{id}/publish", approvedETag);

        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        await AssertProblemAsync(published, HttpStatusCode.NotFound, "NotFound");
        Assert.Equal("Draft", await GetAuthorStatusAsync(authorA, id));
        Assert.Empty((await ReadJsonAsync(await anonymous.GetAsync(PublicPath))).EnumerateArray());
    }

    // Scenario 18
    [Fact]
    public async Task History_OfPublishedPost_ShowsApproverAndPublisherWithTimes()
    {
        var editor1 = _factory.CreateClientFor(_users.Editor1);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, approvedETag) = await CreateApprovedPostAsync(editor1, editor2);
        var approvedAt = _factory.Time.GetUtcNow();
        await SendAsync(editor1, HttpMethod.Post, $"{EditorPath}/{id}/publish", approvedETag);
        var publishedAt = _factory.Time.GetUtcNow();

        var history = await editor2.GetAsync($"{EditorPath}/{id}/history");

        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        var changes = (await ReadJsonAsync(history)).GetProperty("history").EnumerateArray().ToList();
        var approval = changes.Single(change => change.GetProperty("toStatus").GetString() == "Approved");
        Assert.Equal(_users.Editor2.Id, approval.GetProperty("actorId").GetGuid());
        Assert.Equal("Editor 2", approval.GetProperty("actorDisplayName").GetString());
        Assert.Equal(approvedAt, approval.GetProperty("occurredAt").GetDateTimeOffset());
        var publication = changes.Single(change => change.GetProperty("toStatus").GetString() == "Published");
        Assert.Equal(_users.Editor1.Id, publication.GetProperty("actorId").GetGuid());
        Assert.Equal("Editor 1", publication.GetProperty("actorDisplayName").GetString());
        Assert.Equal(publishedAt, publication.GetProperty("occurredAt").GetDateTimeOffset());
    }

    // Scenario 19
    [Fact]
    public async Task EditorLists_ShowApprovedAndPublishedPostsOfAllAuthors()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var authorB = _factory.CreateClientFor(_users.AuthorB);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (approvedA, _) = await CreateApprovedPostAsync(authorA, editor2);
        var (approvedB, _) = await CreateApprovedPostAsync(authorB, editor2);
        var (publishedA, _) = await CreatePublishedPostAsync(authorA, editor2);
        var (publishedB, _) = await CreatePublishedPostAsync(authorB, editor2);
        await CreateSubmittedPostAsync(authorA);

        var approved = (await ReadJsonAsync(await editor2.GetAsync($"{EditorPath}?status=Approved")))
            .EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToList();
        var published = (await ReadJsonAsync(await editor2.GetAsync($"{EditorPath}?status=Published")))
            .EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToList();

        Assert.Equal([approvedB, approvedA], approved);
        Assert.Equal([publishedB, publishedA], published);
    }

    // Scenario 24
    [Fact]
    public async Task Publish_WithoutIfMatch_RequiresPrecondition()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, _) = await CreateApprovedPostAsync(authorA, editor2);

        var response = await SendAsync(editor2, HttpMethod.Post, $"{EditorPath}/{id}/publish", ifMatch: null);

        await AssertProblemAsync(response, HttpStatusCode.PreconditionRequired, "PreconditionRequired");
        Assert.Equal("Approved", await GetAuthorStatusAsync(authorA, id));
    }

    [Fact]
    public async Task Publish_WithStaleVersion_IsRejectedWithoutChange()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, staleETag) = await CreatePublishedPostAsync(authorA, editor2, "Secret title", "Secret body");
        var unpublished = await SendAsync(editor2, HttpMethod.Post, $"{EditorPath}/{id}/unpublish", staleETag);
        var currentVersion = (await ReadJsonAsync(unpublished)).GetProperty("version").GetGuid();

        var response = await SendAsync(editor2, HttpMethod.Post, $"{EditorPath}/{id}/publish", staleETag);

        var problem = await AssertProblemAsync(response, HttpStatusCode.PreconditionFailed, "StaleVersion");
        Assert.Equal("Approved", problem.GetProperty("currentStatus").GetString());
        Assert.Equal(currentVersion, problem.GetProperty("currentVersion").GetGuid());
        Assert.DoesNotContain("Secret", problem.GetRawText());
        Assert.Equal("Approved", await GetAuthorStatusAsync(authorA, id));
    }

    // Scenario 26
    [Fact]
    public async Task Publish_AlreadyPublishedPostWithCurrentVersion_IsConflictNotStale()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, etag) = await CreatePublishedPostAsync(authorA, editor2);
        var historyCount = await GetHistoryCountAsync(editor2, id);

        var response = await SendAsync(editor2, HttpMethod.Post, $"{EditorPath}/{id}/publish", etag);

        var problem = await AssertProblemAsync(response, HttpStatusCode.Conflict, "InvalidStatusTransition");
        Assert.Equal("Published", problem.GetProperty("currentStatus").GetString());
        Assert.Equal(historyCount, await GetHistoryCountAsync(editor2, id));
    }

    private static async Task<(Guid Id, string? ETag)> CreatePostAsync(
        HttpClient client,
        string title = "Title",
        string body = "Body")
    {
        var response = await client.PostAsJsonAsync(AuthorPath, new { title, body });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return ((await ReadJsonAsync(response)).GetProperty("id").GetGuid(), response.Headers.ETag?.Tag);
    }

    private async Task<(Guid Id, string? ETag)> CreateSubmittedPostAsync(
        HttpClient author,
        string title = "Title",
        string body = "Body")
    {
        var (id, etag) = await CreatePostAsync(author, title, body);
        return (id, await SendExpectingOkAsync(author, $"{AuthorPath}/{id}/submit", etag));
    }

    private async Task<(Guid Id, string? ETag)> CreateApprovedPostAsync(
        HttpClient author,
        HttpClient editor,
        string title = "Title",
        string body = "Body")
    {
        var (id, etag) = await CreateSubmittedPostAsync(author, title, body);
        return (id, await SendExpectingOkAsync(editor, $"{EditorPath}/{id}/approve", etag));
    }

    private async Task<(Guid Id, string? ETag)> CreatePublishedPostAsync(
        HttpClient author,
        HttpClient editor,
        string title = "Title",
        string body = "Body")
    {
        var (id, etag) = await CreateApprovedPostAsync(author, editor, title, body);
        return (id, await SendExpectingOkAsync(editor, $"{EditorPath}/{id}/publish", etag));
    }

    private async Task<string?> SendExpectingOkAsync(HttpClient client, string path, string? ifMatch)
    {
        var response = await SendAsync(client, HttpMethod.Post, path, ifMatch);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response.Headers.ETag?.Tag;
    }

    private Task<HttpResponseMessage> EditWithConfirmationAsync(HttpClient author, Guid id)
    {
        _factory.Time.Advance(TimeSpan.FromSeconds(1));
        return author.PutAsJsonAsync(
            $"{AuthorPath}/{id}",
            new { title = "Changed title", body = "Changed body", confirmRevertToDraft = true });
    }

    private static async Task<string?> GetAuthorStatusAsync(HttpClient author, Guid id) =>
        (await ReadJsonAsync(await author.GetAsync($"{AuthorPath}/{id}"))).GetProperty("status").GetString();

    private static async Task<int> GetHistoryCountAsync(HttpClient editor, Guid id) =>
        (await ReadJsonAsync(await editor.GetAsync($"{EditorPath}/{id}/history"))).GetProperty("history").GetArrayLength();

    private Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? ifMatch)
    {
        // History and lists are ordered by time, so every command gets its own timestamp.
        _factory.Time.Advance(TimeSpan.FromSeconds(1));
        var request = new HttpRequestMessage(method, path);

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return client.SendAsync(request);
    }

    private static async Task<JsonElement> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await ReadJsonAsync(response);
        Assert.Equal(expectedCode, problem.GetProperty("code").GetString());
        return problem;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();
}
