using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BlogSpecLab.Tests.Infra.AI;

/// <summary>
/// A fake HTTP transport for the Responses API. Records each request and answers with a programmed response, so
/// that no test reaches the real provider.
/// </summary>
internal sealed class StubProviderHandler : HttpMessageHandler
{
    private Func<CancellationToken, Task<HttpResponseMessage>> _respond =
        _ => Task.FromResult(Json(HttpStatusCode.OK, Response(MessageOutput(OutputText(ValidOutputJson())))));

    public List<(HttpMethod Method, Uri? Uri, string? Authorization, JsonNode Body)> Requests { get; } = [];

    public void RespondWith(HttpStatusCode status, JsonNode body) =>
        _respond = _ => Task.FromResult(Json(status, body));

    public void RespondWith(Func<CancellationToken, Task<HttpResponseMessage>> respond) => _respond = respond;

    public void Throw(Exception exception) => _respond = _ => Task.FromException<HttpResponseMessage>(exception);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var content = request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, request.RequestUri, request.Headers.Authorization?.ToString(), JsonNode.Parse(content)!));
        return await _respond(cancellationToken);
    }

    public static string ValidOutputJson(string outcome = "completed", string issues = "[]", string missing = "[]") =>
        $$"""{"outcome":"{{outcome}}","issues":{{issues}},"missing_information":{{missing}}}""";

    public static JsonObject Response(params JsonNode[] output) => Response("completed", output);

    public static JsonObject Response(string status, JsonNode[] output, JsonNode? incompleteDetails = null, JsonNode? error = null) =>
        new()
        {
            ["id"] = "resp_test_1",
            ["object"] = "response",
            ["created_at"] = 1_767_225_600,
            ["status"] = status,
            ["model"] = "grok-4.7",
            ["output"] = new JsonArray(output),
            ["error"] = error,
            ["incomplete_details"] = incompleteDetails,
            ["instructions"] = null,
            ["metadata"] = new JsonObject(),
            ["parallel_tool_calls"] = false,
            ["tool_choice"] = "auto",
            ["tools"] = new JsonArray(),
            ["usage"] = new JsonObject
            {
                ["input_tokens"] = 120,
                ["input_tokens_details"] = new JsonObject { ["cached_tokens"] = 0 },
                ["output_tokens"] = 40,
                ["output_tokens_details"] = new JsonObject { ["reasoning_tokens"] = 0 },
                ["total_tokens"] = 160
            }
        };

    public static JsonObject MessageOutput(params JsonNode[] content) =>
        new()
        {
            ["type"] = "message",
            ["id"] = "msg_test_" + Guid.NewGuid().ToString("N"),
            ["status"] = "completed",
            ["role"] = "assistant",
            ["content"] = new JsonArray(content)
        };

    public static JsonObject OutputText(string text) =>
        new() { ["type"] = "output_text", ["text"] = text, ["annotations"] = new JsonArray() };

    public static JsonObject Refusal(string text) => new() { ["type"] = "refusal", ["refusal"] = text };

    public static JsonObject ReasoningOutput() =>
        new() { ["type"] = "reasoning", ["id"] = "rs_test_1", ["summary"] = new JsonArray() };

    public static JsonObject FunctionCallOutput() =>
        new()
        {
            ["type"] = "function_call",
            ["id"] = "fc_test_1",
            ["call_id"] = "call_test_1",
            ["name"] = "publish_post",
            ["arguments"] = "{}",
            ["status"] = "completed"
        };

    public static JsonObject ErrorBody(string message) =>
        new() { ["error"] = new JsonObject { ["message"] = message, ["type"] = "error", ["code"] = "error" } };

    private static HttpResponseMessage Json(HttpStatusCode status, JsonNode body) =>
        new(status) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };

    /// <summary>The text of the single user message sent to the provider, whatever content shape the SDK used.</summary>
    public static string UserMessageText(JsonNode requestBody)
    {
        var input = requestBody["input"]!.AsArray();
        var message = Assert.Single(input)!;
        Assert.Equal("user", message["role"]!.GetValue<string>());

        var content = message["content"]!;

        if (content is JsonValue)
        {
            return content.GetValue<string>();
        }

        var part = Assert.Single(content.AsArray())!;
        return part["text"]!.GetValue<string>();
    }

    public static JsonDocument Parse(string json) => JsonDocument.Parse(json);
}
