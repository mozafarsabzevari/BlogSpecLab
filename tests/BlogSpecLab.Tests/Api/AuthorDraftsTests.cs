using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Infra.DataAccess;
using BlogSpecLab.Tests.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlogSpecLab.Tests.Api;

public class AuthorDraftsTests : IAsyncLifetime
{
    private const string PostsPath = "/api/author/posts";

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

    // Scenario 1
    [Fact]
    public async Task Author_CreatesEditsAndListsOwnDrafts()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var authorB = _factory.CreateClientFor(_users.AuthorB);

        var created = await authorA.PostAsJsonAsync(PostsPath, new { title = "First", body = "Body" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var post = await ReadJsonAsync(created);
        var id = post.GetProperty("id").GetGuid();
        Assert.Equal($"{PostsPath}/{id}", created.Headers.Location?.OriginalString);
        Assert.Equal("Draft", post.GetProperty("status").GetString());
        Assert.Equal("First", post.GetProperty("title").GetString());
        Assert.Equal("Body", post.GetProperty("body").GetString());
        AssertETagMatchesVersion(created, post);

        _factory.Time.Advance(TimeSpan.FromMinutes(1));
        var edited = await authorA.PutAsJsonAsync($"{PostsPath}/{id}", new { title = "First edited", body = "Body 2" });

        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var editedPost = await ReadJsonAsync(edited);
        Assert.Equal("First edited", editedPost.GetProperty("title").GetString());
        Assert.Equal("Draft", editedPost.GetProperty("status").GetString());
        Assert.NotEqual(post.GetProperty("version").GetGuid(), editedPost.GetProperty("version").GetGuid());
        AssertETagMatchesVersion(edited, editedPost);

        await authorB.PostAsJsonAsync(PostsPath, new { title = "Other author", body = "Body" });

        var list = await authorA.GetAsync(PostsPath);

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var item = Assert.Single((await ReadJsonAsync(list)).EnumerateArray());
        Assert.Equal(id, item.GetProperty("id").GetGuid());
        Assert.Equal("First edited", item.GetProperty("title").GetString());
        Assert.Equal("Draft", item.GetProperty("status").GetString());
        Assert.Equal(editedPost.GetProperty("version").GetGuid(), item.GetProperty("version").GetGuid());
    }

    [Fact]
    public async Task List_ShowsMostRecentlyChangedPostFirst()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var older = await CreatePostAsync(authorA, "Older");
        _factory.Time.Advance(TimeSpan.FromMinutes(1));
        var newer = await CreatePostAsync(authorA, "Newer");
        _factory.Time.Advance(TimeSpan.FromMinutes(1));

        await authorA.PutAsJsonAsync($"{PostsPath}/{older}", new { title = "Older edited", body = "Body" });

        var ids = (await ReadJsonAsync(await authorA.GetAsync(PostsPath)))
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToList();
        Assert.Equal([older, newer], ids);
    }

    // Scenario 2
    [Fact]
    public async Task OtherAuthor_CannotViewEditOrDeletePost()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var authorB = _factory.CreateClientFor(_users.AuthorB);
        var created = await authorA.PostAsJsonAsync(PostsPath, new { title = "Private title", body = "Private body" });
        var post = await ReadJsonAsync(created);
        var id = post.GetProperty("id").GetGuid();

        var get = await authorB.GetAsync($"{PostsPath}/{id}");
        var put = await authorB.PutAsJsonAsync($"{PostsPath}/{id}", new { title = "Hijack", body = "Hijack" });
        var delete = await SendDeleteAsync(authorB, id, created.Headers.ETag?.Tag);

        foreach (var response in new[] { get, put, delete })
        {
            await AssertProblemAsync(response, HttpStatusCode.NotFound, "NotFound");
            var content = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Private", content);
        }

        var stillThere = await ReadJsonAsync(await authorA.GetAsync($"{PostsPath}/{id}"));
        Assert.Equal("Private title", stillThere.GetProperty("title").GetString());
    }

    // Scenario 3
    [Fact]
    public async Task Create_RejectsTooLongTitle()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);

        var response = await authorA.PostAsJsonAsync(PostsPath, new { title = new string('ع', 201), body = "Body" });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "InvalidTitle");
    }

    // Scenario 3
    [Fact]
    public async Task Create_RejectsTooLongBody()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);

        var response = await authorA.PostAsJsonAsync(PostsPath, new { title = "Title", body = new string('م', 50_001) });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "InvalidBody");
    }

    // Scenario 20
    [Fact]
    public async Task AuthorRoutes_RejectMissingOrUnknownToken()
    {
        var anonymous = _factory.CreateClient();
        var unknown = _factory.CreateClientWithToken("not-a-real-token");

        await AssertProblemAsync(await anonymous.GetAsync(PostsPath), HttpStatusCode.Unauthorized, "Unauthenticated");
        await AssertProblemAsync(await unknown.GetAsync(PostsPath), HttpStatusCode.Unauthorized, "Unauthenticated");
    }

    // Scenario 20
    [Fact]
    public async Task AuthorRoutes_RejectExpiredToken()
    {
        var shortLived = await TestUsers.CreateUserAsync(_factory, "Short lived", [Role.Author], TimeSpan.FromDays(1));
        var client = _factory.CreateClientFor(shortLived);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(PostsPath)).StatusCode);

        _factory.Time.Advance(TimeSpan.FromDays(2));

        await AssertProblemAsync(await client.GetAsync(PostsPath), HttpStatusCode.Unauthorized, "Unauthenticated");
    }

    // Scenario 23
    [Fact]
    public async Task AuthorRoutes_RejectTokenRemovedFromDatabase()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        Assert.Equal(HttpStatusCode.OK, (await authorA.GetAsync(PostsPath)).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<BlogSpecLabDbContext>().AccessTokens
                .Where(token => token.UserId == _users.AuthorA.Id)
                .ExecuteDeleteAsync();
        }

        await AssertProblemAsync(await authorA.GetAsync(PostsPath), HttpStatusCode.Unauthorized, "Unauthenticated");
    }

    [Fact]
    public async Task UserWithoutAuthorRole_IsForbidden()
    {
        var editorOnly = _factory.CreateClientFor(_users.Editor2);

        await AssertProblemAsync(
            await editorOnly.PostAsJsonAsync(PostsPath, new { title = "Title", body = "Body" }),
            HttpStatusCode.Forbidden,
            "Forbidden");
        await AssertProblemAsync(await editorOnly.GetAsync(PostsPath), HttpStatusCode.Forbidden, "Forbidden");
    }

    [Fact]
    public async Task Get_ReturnsPostWithETag()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var id = await CreatePostAsync(authorA, "Title");

        var response = await authorA.GetAsync($"{PostsPath}/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var post = await ReadJsonAsync(response);
        Assert.Equal(id, post.GetProperty("id").GetGuid());
        AssertETagMatchesVersion(response, post);
    }

    [Fact]
    public async Task Delete_NeverPublishedDraft_RemovesIt()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var created = await authorA.PostAsJsonAsync(PostsPath, new { title = "To delete", body = "Body" });
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        var response = await SendDeleteAsync(authorA, id, created.Headers.ETag?.Tag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await authorA.GetAsync($"{PostsPath}/{id}")).StatusCode);
        Assert.Empty((await ReadJsonAsync(await authorA.GetAsync(PostsPath))).EnumerateArray());
    }

    [Fact]
    public async Task Delete_WithoutIfMatch_RequiresPrecondition()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var id = await CreatePostAsync(authorA, "Title");

        var response = await SendDeleteAsync(authorA, id, ifMatch: null);

        await AssertProblemAsync(response, HttpStatusCode.PreconditionRequired, "PreconditionRequired");
        Assert.Equal(HttpStatusCode.OK, (await authorA.GetAsync($"{PostsPath}/{id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_WithMalformedIfMatch_IsBadRequest()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var id = await CreatePostAsync(authorA, "Title");

        var response = await SendDeleteAsync(authorA, id, "\"not-a-guid\"");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "InvalidIfMatch");
    }

    [Fact]
    public async Task Delete_WithStaleVersion_IsRejectedWithCurrentState()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var created = await authorA.PostAsJsonAsync(PostsPath, new { title = "Secret title", body = "Secret body" });
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();
        var staleETag = created.Headers.ETag?.Tag;
        var edited = await authorA.PutAsJsonAsync($"{PostsPath}/{id}", new { title = "Secret title 2", body = "Secret body" });
        var currentVersion = (await ReadJsonAsync(edited)).GetProperty("version").GetGuid();

        var response = await SendDeleteAsync(authorA, id, staleETag);

        var problem = await AssertProblemAsync(response, HttpStatusCode.PreconditionFailed, "StaleVersion");
        Assert.Equal("Draft", problem.GetProperty("currentStatus").GetString());
        Assert.Equal(currentVersion, problem.GetProperty("currentVersion").GetGuid());
        Assert.DoesNotContain("Secret", problem.GetRawText());
        Assert.Equal(HttpStatusCode.OK, (await authorA.GetAsync($"{PostsPath}/{id}")).StatusCode);
    }

    private static async Task<Guid> CreatePostAsync(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync(PostsPath, new { title, body = "Body" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> SendDeleteAsync(HttpClient client, Guid id, string? ifMatch)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"{PostsPath}/{id}");

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return client.SendAsync(request);
    }

    private static void AssertETagMatchesVersion(HttpResponseMessage response, JsonElement post)
    {
        Assert.Equal(
            new EntityTagHeaderValue($"\"{post.GetProperty("version").GetGuid()}\""),
            response.Headers.ETag);
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
