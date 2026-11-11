using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BlogSpecLab.Tests.Api.Infrastructure;

/// <summary>HTTP helpers shared by the content review acceptance tests.</summary>
internal static class ContentReviewApi
{
    public const string PostsPath = "/api/author/posts";
    public const string EditorPath = "/api/editor/posts";

    public const string DefaultTitle = "عنوان پست آزمایشی";
    public const string DefaultBody = "ما میخواهیم این متن را بررسی کنیم. جملهٔ دوم هم اینجاست.";

    public static string ReviewsPath(Guid postId) => $"{PostsPath}/{postId}/content-reviews";

    public static async Task<(Guid Id, string ETag)> CreatePostAsync(
        HttpClient author,
        string title = DefaultTitle,
        string body = DefaultBody)
    {
        var response = await author.PostAsJsonAsync(PostsPath, new { title, body });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return ((await ReadJsonAsync(response)).GetProperty("id").GetGuid(), response.Headers.ETag!.Tag);
    }

    public static Task<HttpResponseMessage> RequestReviewAsync(HttpClient client, Guid postId) =>
        client.PostAsync(ReviewsPath(postId), content: null);

    public static async Task<JsonElement> RequestReviewSucceedsAsync(HttpClient client, Guid postId)
    {
        var response = await RequestReviewAsync(client, postId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    public static async Task<List<JsonElement>> ListReviewsAsync(HttpClient client, Guid postId)
    {
        var response = await client.GetAsync(ReviewsPath(postId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadJsonAsync(response)).EnumerateArray().ToList();
    }

    public static async Task<string> EditAsync(HttpClient author, Guid postId, string title, string body)
    {
        var response = await author.PutAsJsonAsync($"{PostsPath}/{postId}", new { title, body });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response.Headers.ETag!.Tag;
    }

    public static Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string? ifMatch,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, path);

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request);
    }

    /// <summary>Moves a draft to InReview and returns the new ETag.</summary>
    public static async Task<string> SubmitAsync(HttpClient author, Guid postId, string etag)
    {
        var response = await SendAsync(author, HttpMethod.Post, $"{PostsPath}/{postId}/submit", etag);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response.Headers.ETag!.Tag;
    }

    public static async Task<string> EditorActionAsync(HttpClient editor, Guid postId, string action, string etag, object? body = null)
    {
        var response = await SendAsync(editor, HttpMethod.Post, $"{EditorPath}/{postId}/{action}", etag, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response.Headers.ETag!.Tag;
    }

    public static async Task<JsonElement> GetOwnPostAsync(HttpClient author, Guid postId)
    {
        var response = await author.GetAsync($"{PostsPath}/{postId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    public static async Task<JsonElement> AssertProblemAsync(
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

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>
    /// Every decoded string in a JSON value. Raw JSON escapes non-ASCII text, so content checks must use this.
    /// </summary>
    public static string AllText(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.Object => string.Join("\n", element.EnumerateObject().Select(property => AllText(property.Value))),
            JsonValueKind.Array => string.Join("\n", element.EnumerateArray().Select(AllText)),
            _ => string.Empty
        };
}
