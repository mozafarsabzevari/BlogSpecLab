using System.Net;
using System.Text.Json.Nodes;
using BlogSpecLab.Core.Application.ContentReviews;
using BlogSpecLab.Infra.AI.ContentReviews;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlogSpecLab.Tests.Infra.AI;

/// <summary>Request shape and response mapping of the xAI adapter, over a fake HTTP transport.</summary>
public class ResponsesContentReviewModelTests
{
    private const string FakeKey = "test-key-not-a-secret";

    private readonly StubProviderHandler _handler = new();

    private ResponsesContentReviewModel CreateModel(string? apiKey = FakeKey) =>
        new(
            new ContentReviewModelOptions { ApiKey = apiKey },
            NullLogger<ResponsesContentReviewModel>.Instance,
            TimeProvider.System,
            new HttpClient(_handler));

    private Task<ContentReviewModelOutcome> ReviewAsync(CancellationToken cancellationToken = default) =>
        CreateModel().ReviewAsync(new ContentReviewInput("عنوان پست", "متن پست"), cancellationToken);

    // R-01, R-03, R-05, R-09
    [Fact]
    public async Task Request_UsesConfiguredModelStrictSchemaNoStoreNoToolsAndOnlyTitleAndBody()
    {
        await ReviewAsync();

        var (method, uri, authorization, body) = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("https://api.x.ai/v1/responses", uri?.GetLeftPart(UriPartial.Path));
        Assert.Equal($"Bearer {FakeKey}", authorization);

        Assert.Equal("grok-4.7", body["model"]!.GetValue<string>());
        Assert.False(body["store"]!.GetValue<bool>());
        Assert.Equal(8000, body["max_output_tokens"]!.GetValue<int>());
        Assert.True(body["stream"] is null || !body["stream"]!.GetValue<bool>());
        Assert.True(body["tools"] is null || body["tools"]!.AsArray().Count == 0);
        Assert.Null(body["previous_response_id"]);
        Assert.Equal(ContentReviewPrompt.Instructions, body["instructions"]!.GetValue<string>());

        var format = body["text"]!["format"]!;
        Assert.Equal("json_schema", format["type"]!.GetValue<string>());
        Assert.Equal("content_review_v1", format["name"]!.GetValue<string>());
        Assert.True(format["strict"]!.GetValue<bool>());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(ContentReviewSchema.Json.ToString()), format["schema"]));

        var sent = JsonNode.Parse(StubProviderHandler.UserMessageText(body))!.AsObject();
        Assert.Equal(["title", "body"], sent.Select(property => property.Key));
        Assert.Equal("عنوان پست", sent["title"]!.GetValue<string>());
        Assert.Equal("متن پست", sent["body"]!.GetValue<string>());
    }

    [Fact]
    public async Task CompletedResponse_BecomesCandidateWithProviderModelName()
    {
        var issues = """[{"category":"clarity","severity":"high","location":"body","excerpt":"متن","explanation":"توضیح","suggestion":null}]""";
        _handler.RespondWith(HttpStatusCode.OK, StubProviderHandler.Response(
            StubProviderHandler.ReasoningOutput(),
            StubProviderHandler.MessageOutput(StubProviderHandler.OutputText(StubProviderHandler.ValidOutputJson(issues: issues)))));

        var outcome = await ReviewAsync();

        var candidate = Assert.IsType<ContentReviewModelOutcome.Candidate>(outcome).Review;
        Assert.Equal("completed", candidate.Outcome);
        Assert.Equal(new CandidateIssue("clarity", "high", "body", "متن", "توضیح", null), Assert.Single(candidate.Issues));
        Assert.Empty(candidate.MissingInformation);
        Assert.Equal("grok-4.7", candidate.ModelName);
        Assert.Equal(ContentReviewPrompt.Version, candidate.PromptVersion);
        Assert.Equal(ContentReviewSchema.Name, candidate.SchemaVersion);
    }

    [Fact]
    public async Task NeedsInputResponse_KeepsMissingInformation()
    {
        _handler.RespondWith(HttpStatusCode.OK, StubProviderHandler.Response(StubProviderHandler.MessageOutput(
            StubProviderHandler.OutputText(StubProviderHandler.ValidOutputJson("needs_input", missing: """[{"description":"موضوع روشن نیست"}]""")))));

        var candidate = Assert.IsType<ContentReviewModelOutcome.Candidate>(await ReviewAsync()).Review;

        Assert.Equal("needs_input", candidate.Outcome);
        Assert.Equal(["موضوع روشن نیست"], candidate.MissingInformation);
    }

    [Fact]
    public async Task Refusal_IsRefused()
    {
        _handler.RespondWith(HttpStatusCode.OK, StubProviderHandler.Response(
            StubProviderHandler.MessageOutput(StubProviderHandler.Refusal("I can't help with that."))));

        Assert.IsType<ContentReviewModelOutcome.Refused>(await ReviewAsync());
    }

    [Theory]
    [InlineData("max_output_tokens")]
    [InlineData("content_filter")]
    public async Task IncompleteResponse_IsInvalidOutput(string reason)
    {
        _handler.RespondWith(HttpStatusCode.OK, StubProviderHandler.Response(
            "incomplete",
            [StubProviderHandler.MessageOutput(StubProviderHandler.OutputText("""{"outcome":"comp"""))],
            new JsonObject { ["reason"] = reason }));

        Assert.IsType<ContentReviewModelOutcome.InvalidOutput>(await ReviewAsync());
    }

    [Fact]
    public async Task FailedResponse_IsInvalidOutput()
    {
        _handler.RespondWith(HttpStatusCode.OK, StubProviderHandler.Response(
            "failed",
            [],
            error: new JsonObject { ["code"] = "server_error", ["message"] = "failure" }));

        Assert.IsType<ContentReviewModelOutcome.InvalidOutput>(await ReviewAsync());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"outcome":"completed","issues":[]}""")]
    [InlineData("""{"outcome":"completed","issues":[],"missing_information":[],"extra":1}""")]
    [InlineData("""{"outcome":null,"issues":[],"missing_information":[]}""")]
    [InlineData("""{"outcome":"completed","issues":[{"category":"clarity"}],"missing_information":[]}""")]
    [InlineData("""{"outcome":"completed","issues":[{"category":"clarity","severity":"low","location":"body","excerpt":"x","explanation":"y"}],"missing_information":[]}""")]
    public async Task OutputNotMatchingSchema_IsInvalidOutput(string text)
    {
        _handler.RespondWith(HttpStatusCode.OK, StubProviderHandler.Response(
            StubProviderHandler.MessageOutput(StubProviderHandler.OutputText(text))));

        Assert.IsType<ContentReviewModelOutcome.InvalidOutput>(await ReviewAsync());
    }

    public static TheoryData<string> UnexpectedForms => new() { "tool_call", "two_messages", "no_message", "two_parts" };

    [Theory]
    [MemberData(nameof(UnexpectedForms))]
    public async Task OutputOtherThanOneTextMessage_IsUnexpectedOutputForm(string form)
    {
        var text = StubProviderHandler.OutputText(StubProviderHandler.ValidOutputJson());
        JsonNode[] output = form switch
        {
            "tool_call" => [StubProviderHandler.FunctionCallOutput()],
            "two_messages" => [StubProviderHandler.MessageOutput(text), StubProviderHandler.MessageOutput(text.DeepClone())],
            "no_message" => [StubProviderHandler.ReasoningOutput()],
            _ => [StubProviderHandler.MessageOutput(text, text.DeepClone())]
        };
        _handler.RespondWith(HttpStatusCode.OK, StubProviderHandler.Response(output));

        Assert.IsType<ContentReviewModelOutcome.UnexpectedOutputForm>(await ReviewAsync());
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ProviderErrorStatus_IsProviderUnavailableWithoutRetry(HttpStatusCode status)
    {
        _handler.RespondWith(status, StubProviderHandler.ErrorBody("provider error"));

        Assert.IsType<ContentReviewModelOutcome.ProviderUnavailable>(await ReviewAsync());
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task NetworkError_IsProviderUnavailable()
    {
        _handler.Throw(new HttpRequestException("connection refused"));

        Assert.IsType<ContentReviewModelOutcome.ProviderUnavailable>(await ReviewAsync());
    }

    // R-13
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task MissingApiKey_IsProviderUnavailableWithoutAnyRequest(string? apiKey)
    {
        var outcome = await CreateModel(apiKey).ReviewAsync(new ContentReviewInput("عنوان", "متن"));

        Assert.IsType<ContentReviewModelOutcome.ProviderUnavailable>(outcome);
        Assert.Empty(_handler.Requests);
    }

    // contracts/application-ai.md: cancellation is never turned into an outcome.
    [Fact]
    public async Task Cancellation_PropagatesAsOperationCanceled()
    {
        _handler.RespondWith(async cancellationToken =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        });
        using var cancellation = new CancellationTokenSource();

        var review = ReviewAsync(cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => review);
    }
}
