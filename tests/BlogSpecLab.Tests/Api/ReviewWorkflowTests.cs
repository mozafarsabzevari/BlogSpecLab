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

public class ReviewWorkflowTests : IAsyncLifetime
{
    private const string AuthorPath = "/api/author/posts";
    private const string EditorPath = "/api/editor/posts";

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

    // Scenario 4
    [Fact]
    public async Task Author_SubmitsAndWithdrawsPost()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var (id, etag) = await CreatePostAsync(authorA);

        var submitted = await SendAsync(authorA, HttpMethod.Post, $"{AuthorPath}/{id}/submit", etag);

        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var submittedPost = await ReadJsonAsync(submitted);
        Assert.Equal("InReview", submittedPost.GetProperty("status").GetString());
        AssertETagMatchesVersion(submitted, submittedPost);

        var withdrawn = await SendAsync(authorA, HttpMethod.Post, $"{AuthorPath}/{id}/withdraw", submitted.Headers.ETag?.Tag);

        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        var withdrawnPost = await ReadJsonAsync(withdrawn);
        Assert.Equal("Draft", withdrawnPost.GetProperty("status").GetString());
        AssertETagMatchesVersion(withdrawn, withdrawnPost);
    }

    [Fact]
    public async Task Submit_WithoutIfMatch_RequiresPrecondition()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var (id, _) = await CreatePostAsync(authorA);

        var response = await SendAsync(authorA, HttpMethod.Post, $"{AuthorPath}/{id}/submit", ifMatch: null);

        await AssertProblemAsync(response, HttpStatusCode.PreconditionRequired, "PreconditionRequired");
        Assert.Equal("Draft", await GetAuthorStatusAsync(authorA, id));
    }

    [Fact]
    public async Task OtherAuthor_CannotSubmitPost()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var authorB = _factory.CreateClientFor(_users.AuthorB);
        var (id, etag) = await CreatePostAsync(authorA);

        var response = await SendAsync(authorB, HttpMethod.Post, $"{AuthorPath}/{id}/submit", etag);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "NotFound");
        Assert.Equal("Draft", await GetAuthorStatusAsync(authorA, id));
    }

    // Scenario 5 and SC-006
    [Fact]
    public async Task Editor_ReturnsPostWithReason_ShownInAuthorList()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, etag) = await CreateSubmittedPostAsync(authorA);

        var returned = await SendAsync(editor2, HttpMethod.Post, $"{EditorPath}/{id}/return", etag, new { reason = "Add sources" });

        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        var returnedPost = await ReadJsonAsync(returned);
        Assert.Equal("Returned", returnedPost.GetProperty("status").GetString());
        AssertETagMatchesVersion(returned, returnedPost);

        var item = Assert.Single((await ReadJsonAsync(await authorA.GetAsync(AuthorPath))).EnumerateArray());
        Assert.Equal("Returned", item.GetProperty("status").GetString());
        Assert.Equal("Add sources", item.GetProperty("lastReturnReason").GetString());
    }

    // Scenario 6
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Return_WithoutReason_IsRejected(string reason)
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, etag) = await CreateSubmittedPostAsync(authorA);

        var response = await SendAsync(editor2, HttpMethod.Post, $"{EditorPath}/{id}/return", etag, new { reason });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "ReturnReasonRequired");
        Assert.Equal("InReview", await GetAuthorStatusAsync(authorA, id));
    }

    [Fact]
    public async Task Editor_ApprovesPostOfAnotherAuthor()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, etag) = await CreateSubmittedPostAsync(authorA);

        var approved = await SendAsync(editor2, HttpMethod.Post, $"{EditorPath}/{id}/approve", etag);

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var post = await ReadJsonAsync(approved);
        Assert.Equal("Approved", post.GetProperty("status").GetString());
        AssertETagMatchesVersion(approved, post);
        var lastChange = post.GetProperty("history").EnumerateArray().Last();
        Assert.Equal(_users.Editor2.Id, lastChange.GetProperty("actorId").GetGuid());
        Assert.Equal("Editor 2", lastChange.GetProperty("actorDisplayName").GetString());
        Assert.Equal("InReview", lastChange.GetProperty("fromStatus").GetString());
        Assert.Equal("Approved", lastChange.GetProperty("toStatus").GetString());
    }

    // Scenario 7
    [Fact]
    public async Task Editor_CannotApproveOwnPost()
    {
        var editor1 = _factory.CreateClientFor(_users.Editor1);
        var (id, etag) = await CreateSubmittedPostAsync(editor1);

        var response = await SendAsync(editor1, HttpMethod.Post, $"{EditorPath}/{id}/approve", etag);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "SelfApprovalNotAllowed");
        Assert.Equal("InReview", await GetAuthorStatusAsync(editor1, id));
    }

    [Fact]
    public async Task Editor_CanReturnOwnPost()
    {
        var editor1 = _factory.CreateClientFor(_users.Editor1);
        var (id, etag) = await CreateSubmittedPostAsync(editor1);

        var response = await SendAsync(editor1, HttpMethod.Post, $"{EditorPath}/{id}/return", etag, new { reason = "Rework" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Returned", await GetAuthorStatusAsync(editor1, id));
    }

    // Scenario 9 (publish is covered with US3)
    [Fact]
    public async Task AuthorWithoutEditorRole_CannotApprove()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var authorB = _factory.CreateClientFor(_users.AuthorB);
        var (id, etag) = await CreateSubmittedPostAsync(authorA);

        var response = await SendAsync(authorB, HttpMethod.Post, $"{EditorPath}/{id}/approve", etag);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "Forbidden");
        Assert.Equal("InReview", await GetAuthorStatusAsync(authorA, id));
    }

    // Scenario 16
    [Fact]
    public async Task SecondDecisionWithSameVersion_IsRejectedWithoutChange()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor1 = _factory.CreateClientFor(_users.Editor1);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, etag) = await CreateSubmittedPostAsync(authorA);

        var approved = await SendAsync(editor2, HttpMethod.Post, $"{EditorPath}/{id}/approve", etag);
        var returned = await SendAsync(editor1, HttpMethod.Post, $"{EditorPath}/{id}/return", etag, new { reason = "Late" });

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var problem = await AssertProblemAsync(returned, HttpStatusCode.PreconditionFailed, "StaleVersion");
        Assert.Equal("Approved", problem.GetProperty("currentStatus").GetString());
        var detail = await ReadJsonAsync(await editor2.GetAsync($"{EditorPath}/{id}"));
        Assert.Equal("Approved", detail.GetProperty("status").GetString());
        Assert.DoesNotContain(
            detail.GetProperty("history").EnumerateArray(),
            change => change.GetProperty("toStatus").GetString() == "Returned");
    }

    // Scenario 21
    [Fact]
    public async Task ClientRoleClaims_AreIgnored()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var authorB = _factory.CreateClientFor(_users.AuthorB);
        var (id, etag) = await CreateSubmittedPostAsync(authorB);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{EditorPath}/{id}/approve")
        {
            Content = JsonContent.Create(new { role = "Editor", roles = new[] { "Editor" } })
        };
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        request.Headers.TryAddWithoutValidation("X-Role", "Editor");
        request.Headers.TryAddWithoutValidation("X-User-Roles", "Editor");

        await AssertProblemAsync(await authorA.SendAsync(request), HttpStatusCode.Forbidden, "Forbidden");
        Assert.Equal("InReview", await GetAuthorStatusAsync(authorB, id));
    }

    // Scenario 22
    [Fact]
    public async Task EditorRoleRemovedFromDatabase_IsForbiddenOnNextRequest()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, etag) = await CreateSubmittedPostAsync(authorA);
        Assert.Equal(HttpStatusCode.OK, (await editor2.GetAsync($"{EditorPath}?status=InReview")).StatusCode);

        await RemoveRoleAsync(_users.Editor2.Id, Role.Editor);

        var response = await SendAsync(editor2, HttpMethod.Post, $"{EditorPath}/{id}/approve", etag);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "Forbidden");
        Assert.Equal("InReview", await GetAuthorStatusAsync(authorA, id));
    }

    [Fact]
    public async Task ApprovedPost_StaysApprovedAfterApproverLosesEditorRole()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor1 = _factory.CreateClientFor(_users.Editor1);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, etag) = await CreateSubmittedPostAsync(authorA);
        await SendAsync(editor2, HttpMethod.Post, $"{EditorPath}/{id}/approve", etag);

        await RemoveRoleAsync(_users.Editor2.Id, Role.Editor);

        var detail = await ReadJsonAsync(await editor1.GetAsync($"{EditorPath}/{id}"));
        Assert.Equal("Approved", detail.GetProperty("status").GetString());
        Assert.Equal(_users.Editor2.Id, detail.GetProperty("history").EnumerateArray().Last().GetProperty("actorId").GetGuid());
    }

    // Scenario 25
    [Fact]
    public async Task Approve_WithVersionSeenBeforeAuthorChanges_IsRejectedWithCurrentState()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, staleETag) = await CreateSubmittedPostAsync(authorA, "Secret title", "Secret body");

        var withdrawn = await SendAsync(authorA, HttpMethod.Post, $"{AuthorPath}/{id}/withdraw", staleETag);
        var edited = await authorA.PutAsJsonAsync($"{AuthorPath}/{id}", new { title = "Secret title 2", body = "Secret body 2" });
        var resubmitted = await SendAsync(authorA, HttpMethod.Post, $"{AuthorPath}/{id}/submit", edited.Headers.ETag?.Tag);
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resubmitted.StatusCode);
        var currentVersion = (await ReadJsonAsync(resubmitted)).GetProperty("version").GetGuid();

        var response = await SendAsync(editor2, HttpMethod.Post, $"{EditorPath}/{id}/approve", staleETag);

        var problem = await AssertProblemAsync(response, HttpStatusCode.PreconditionFailed, "StaleVersion");
        Assert.Equal("InReview", problem.GetProperty("currentStatus").GetString());
        Assert.Equal(currentVersion, problem.GetProperty("currentVersion").GetGuid());
        Assert.DoesNotContain("Secret", problem.GetRawText());
        Assert.Equal("InReview", await GetAuthorStatusAsync(authorA, id));
    }

    // Scenario 28
    [Fact]
    public async Task Editor_SeesHistoryOfReturnedPostWithoutContent()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, etag) = await CreateSubmittedPostAsync(authorA, "Secret title", "Secret body");
        _factory.Time.Advance(TimeSpan.FromMinutes(1));
        await SendAsync(editor2, HttpMethod.Post, $"{EditorPath}/{id}/return", etag, new { reason = "Add sources" });

        var history = await editor2.GetAsync($"{EditorPath}/{id}/history");

        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        var body = await ReadJsonAsync(history);
        Assert.Equal(id, body.GetProperty("id").GetGuid());
        Assert.Equal("Returned", body.GetProperty("status").GetString());
        Assert.False(body.TryGetProperty("title", out _));
        Assert.False(body.TryGetProperty("body", out _));
        Assert.DoesNotContain("Secret", body.GetRawText());

        var changes = body.GetProperty("history").EnumerateArray().ToList();
        Assert.Equal(3, changes.Count);
        Assert.Equal("InReview", changes[1].GetProperty("toStatus").GetString());
        Assert.Equal(_users.AuthorA.Id, changes[1].GetProperty("actorId").GetGuid());
        Assert.Equal("Returned", changes[2].GetProperty("toStatus").GetString());
        Assert.Equal("Add sources", changes[2].GetProperty("reason").GetString());
        Assert.Equal("Editor 2", changes[2].GetProperty("actorDisplayName").GetString());

        await AssertProblemAsync(await editor2.GetAsync($"{EditorPath}/{id}"), HttpStatusCode.NotFound, "NotFound");
    }

    [Fact]
    public async Task Edit_PostInReview_IsRejected()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var (id, _) = await CreateSubmittedPostAsync(authorA);

        var response = await authorA.PutAsJsonAsync($"{AuthorPath}/{id}", new { title = "New", body = "New" });

        var problem = await AssertProblemAsync(response, HttpStatusCode.Conflict, "InvalidStatusTransition");
        Assert.Equal("InReview", problem.GetProperty("currentStatus").GetString());
    }

    [Fact]
    public async Task EditorQueue_ListsInReviewPostsOfAllAuthors()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var authorB = _factory.CreateClientFor(_users.AuthorB);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (submittedA, _) = await CreateSubmittedPostAsync(authorA, "From A");
        _factory.Time.Advance(TimeSpan.FromMinutes(1));
        var (submittedB, _) = await CreateSubmittedPostAsync(authorB, "From B");
        await CreatePostAsync(authorA, "Draft only");

        var response = await editor2.GetAsync($"{EditorPath}?status=InReview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await ReadJsonAsync(response)).EnumerateArray().ToList();
        Assert.Equal([submittedB, submittedA], items.Select(item => item.GetProperty("id").GetGuid()));
        Assert.Equal("Author B", items[0].GetProperty("authorDisplayName").GetString());
        Assert.Equal("InReview", items[0].GetProperty("status").GetString());
        Assert.NotEqual(Guid.Empty, items[0].GetProperty("version").GetGuid());
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Returned")]
    [InlineData("Unknown")]
    [InlineData("")]
    public async Task EditorQueue_RejectsInvalidStatus(string status)
    {
        var editor2 = _factory.CreateClientFor(_users.Editor2);

        var response = await editor2.GetAsync($"{EditorPath}?status={status}");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "InvalidStatus");
    }

    [Fact]
    public async Task EditorDetail_HidesDraftButHistoryShowsItWithoutContent()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, _) = await CreatePostAsync(authorA, "Secret title", "Secret body");

        await AssertProblemAsync(await editor2.GetAsync($"{EditorPath}/{id}"), HttpStatusCode.NotFound, "NotFound");

        var history = await editor2.GetAsync($"{EditorPath}/{id}/history");

        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        var body = await ReadJsonAsync(history);
        Assert.Equal("Draft", body.GetProperty("status").GetString());
        Assert.DoesNotContain("Secret", body.GetRawText());
        Assert.Single(body.GetProperty("history").EnumerateArray());
    }

    [Fact]
    public async Task EditorDetail_ShowsInReviewPostWithHistory()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, etag) = await CreateSubmittedPostAsync(authorA, "Title", "Body");

        var response = await editor2.GetAsync($"{EditorPath}/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var post = await ReadJsonAsync(response);
        Assert.Equal("Title", post.GetProperty("title").GetString());
        Assert.Equal("Body", post.GetProperty("body").GetString());
        Assert.Equal(_users.AuthorA.Id, post.GetProperty("authorId").GetGuid());
        Assert.Equal("Author A", post.GetProperty("authorDisplayName").GetString());
        Assert.Equal(2, post.GetProperty("history").GetArrayLength());
        Assert.Equal(new EntityTagHeaderValue(etag!), response.Headers.ETag);
    }

    [Fact]
    public async Task History_OfMissingPost_IsNotFound()
    {
        var editor2 = _factory.CreateClientFor(_users.Editor2);

        var response = await editor2.GetAsync($"{EditorPath}/{Guid.NewGuid()}/history");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "NotFound");
    }

    [Fact]
    public async Task History_ForUserWithoutEditorRole_IsForbidden()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var (id, _) = await CreatePostAsync(authorA);

        var response = await authorA.GetAsync($"{EditorPath}/{id}/history");

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "Forbidden");
    }

    [Fact]
    public async Task Delete_NeverPublishedReturnedPost_RemovesIt()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var editor2 = _factory.CreateClientFor(_users.Editor2);
        var (id, etag) = await CreateSubmittedPostAsync(authorA);
        var returned = await SendAsync(editor2, HttpMethod.Post, $"{EditorPath}/{id}/return", etag, new { reason = "No" });

        var response = await SendAsync(authorA, HttpMethod.Delete, $"{AuthorPath}/{id}", returned.Headers.ETag?.Tag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await authorA.GetAsync($"{AuthorPath}/{id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_PostInReview_IsRejected()
    {
        var authorA = _factory.CreateClientFor(_users.AuthorA);
        var (id, etag) = await CreateSubmittedPostAsync(authorA);

        var response = await SendAsync(authorA, HttpMethod.Delete, $"{AuthorPath}/{id}", etag);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "InvalidStatusTransition");
        Assert.Equal("InReview", await GetAuthorStatusAsync(authorA, id));
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
        HttpClient client,
        string title = "Title",
        string body = "Body")
    {
        var (id, etag) = await CreatePostAsync(client, title, body);
        var response = await SendAsync(client, HttpMethod.Post, $"{AuthorPath}/{id}/submit", etag);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (id, response.Headers.ETag?.Tag);
    }

    private static async Task<string?> GetAuthorStatusAsync(HttpClient author, Guid id) =>
        (await ReadJsonAsync(await author.GetAsync($"{AuthorPath}/{id}"))).GetProperty("status").GetString();

    private async Task RemoveRoleAsync(Guid userId, Role role)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<BlogSpecLabDbContext>().UserRoles
            .Where(userRole => userRole.UserId == userId && userRole.Role == role)
            .ExecuteDeleteAsync();
    }

    private Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string? ifMatch,
        object? body = null)
    {
        // History is ordered by time, so every command gets its own timestamp.
        _factory.Time.Advance(TimeSpan.FromSeconds(1));
        var request = new HttpRequestMessage(method, path);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

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
