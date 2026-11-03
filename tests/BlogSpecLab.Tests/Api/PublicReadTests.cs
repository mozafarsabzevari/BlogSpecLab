using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BlogSpecLab.Tests.Api.Infrastructure;

namespace BlogSpecLab.Tests.Api;

public class PublicReadTests : IAsyncLifetime
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

    // Scenario 11
    [Fact]
    public async Task Anonymous_SeesOnlyPublishedPostsNewestPublicationFirst()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var authorB = _factory.CreateClientFor(_users.AuthorB);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var anonymous = _factory.CreateClient();

        // Created first but published last, so it must come first.
        var (olderPost, olderETag) = await CreateApprovedPostAsync(authorA, editor2, "Older");
        var (newerPost, _) = await CreatePublishedPostAsync(authorB, editor2, "Newer");
        var newerPublishedAt = _factory.Time.GetUtcNow();
        await SendExpectingOkAsync(editor2, $"{EditorPath}/{olderPost}/publish", olderETag);
        var olderPublishedAt = _factory.Time.GetUtcNow();
        await CreatePostAsync(authorA, "Draft");
        await CreateSubmittedPostAsync(authorA, "In review");
        await CreateApprovedPostAsync(authorB, editor2, "Approved");

        var response = await anonymous.GetAsync(PublicPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await ReadJsonAsync(response)).EnumerateArray().ToList();
        Assert.Equal([olderPost, newerPost], items.Select(item => item.GetProperty("id").GetGuid()));
        Assert.Equal("Older", items[0].GetProperty("title").GetString());
        Assert.Equal("Author A", items[0].GetProperty("authorDisplayName").GetString());
        Assert.Equal(olderPublishedAt, items[0].GetProperty("publishedAt").GetDateTimeOffset());
        Assert.Equal(newerPublishedAt, items[1].GetProperty("publishedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task PublicList_ContainsOnlyPublicSummaryFields()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        await CreatePublishedPostAsync(authorA, editor2);

        var item = Assert.Single((await ReadJsonAsync(await _factory.CreateClient().GetAsync(PublicPath))).EnumerateArray());

        Assert.Equal(["authorDisplayName", "id", "publishedAt", "title"], PropertyNames(item));
    }

    [Fact]
    public async Task PublicDetail_ReturnsPublishedPostWithOnlyPublicFields()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, _) = await CreatePublishedPostAsync(authorA, editor2, "Title", "Body");

        var response = await _factory.CreateClient().GetAsync($"{PublicPath}/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var post = await ReadJsonAsync(response);
        Assert.Equal(["authorDisplayName", "body", "id", "publishedAt", "title"], PropertyNames(post));
        Assert.Equal(id, post.GetProperty("id").GetGuid());
        Assert.Equal("Title", post.GetProperty("title").GetString());
        Assert.Equal("Body", post.GetProperty("body").GetString());
        Assert.Equal("Author A", post.GetProperty("authorDisplayName").GetString());
        Assert.Equal(_factory.Time.GetUtcNow(), post.GetProperty("publishedAt").GetDateTimeOffset());
    }

    // Scenario 27
    [Fact]
    public async Task PublicResponses_HaveNoETagOrVersion()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var anonymous = _factory.CreateClient();
        var (id, _) = await CreatePublishedPostAsync(authorA, editor2);

        var list = await anonymous.GetAsync(PublicPath);
        var detail = await anonymous.GetAsync($"{PublicPath}/{id}");

        Assert.Null(list.Headers.ETag);
        Assert.Null(detail.Headers.ETag);
        Assert.False((await ReadJsonAsync(list)).EnumerateArray().Single().TryGetProperty("version", out _));
        Assert.False((await ReadJsonAsync(detail)).TryGetProperty("version", out _));
    }

    [Fact]
    public async Task PublicDetail_MissingAndUnpublishedPostsGetIdenticalNotFound()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var anonymous = _factory.CreateClient();
        var (draft, _) = await CreatePostAsync(authorA, "Secret draft");
        var (inReview, _) = await CreateSubmittedPostAsync(authorA, "Secret review");
        var (approved, _) = await CreateApprovedPostAsync(authorA, editor2, "Secret approved");
        var (unpublished, publishedETag) = await CreatePublishedPostAsync(authorA, editor2, "Secret unpublished");
        await SendExpectingOkAsync(editor2, $"{EditorPath}/{unpublished}/unpublish", publishedETag);

        var missing = await NotFoundShapeAsync(await anonymous.GetAsync($"{PublicPath}/{Guid.NewGuid()}"));

        foreach (var id in new[] { draft, inReview, approved, unpublished })
        {
            var response = await anonymous.GetAsync($"{PublicPath}/{id}");
            var raw = await response.Content.ReadAsStringAsync();
            Assert.Equal(missing, await NotFoundShapeAsync(response));
            Assert.DoesNotContain("Secret", raw);
        }
    }

    [Fact]
    public async Task PublicRoutes_AllowAccessWithoutToken()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync(PublicPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await ReadJsonAsync(response)).EnumerateArray());
    }

    [Fact]
    public async Task PublicRoutes_RejectInvalidToken()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, _) = await CreatePublishedPostAsync(authorA, editor2);
        var unknown = _factory.CreateClientWithToken("not-a-real-token");

        await AssertProblemAsync(await unknown.GetAsync(PublicPath), HttpStatusCode.Unauthorized, "Unauthenticated");
        await AssertProblemAsync(await unknown.GetAsync($"{PublicPath}/{id}"), HttpStatusCode.Unauthorized, "Unauthenticated");
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
        // Publication order is checked, so every command gets its own timestamp.
        _factory.Time.Advance(TimeSpan.FromSeconds(1));
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response.Headers.ETag?.Tag;
    }

    /// <summary>The response body without <c>traceId</c>, which differs on every request.</summary>
    private static async Task<string> NotFoundShapeAsync(HttpResponseMessage response)
    {
        var problem = await AssertProblemAsync(response, HttpStatusCode.NotFound, "NotFound");

        return string.Join(
            "|",
            problem.EnumerateObject()
                .Where(property => property.Name != "traceId")
                .Select(property => $"{property.Name}={property.Value.GetRawText()}"));
    }

    private static List<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToList();

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
