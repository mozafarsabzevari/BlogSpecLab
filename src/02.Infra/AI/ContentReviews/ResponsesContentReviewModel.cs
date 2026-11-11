using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using BlogSpecLab.Core.Application.ContentReviews;
using Microsoft.Extensions.Logging;
using OpenAI.Responses;

namespace BlogSpecLab.Infra.AI.ContentReviews;

/// <summary>
/// Calls the xAI Responses API through the official OpenAI package and turns the response into an SDK-free
/// outcome (contracts/application-ai.md). Non-streaming, <c>store=false</c>, no tools, no automatic retry, no
/// fallback model. Post content and model output are never logged (research.md, R-05).
/// </summary>
public sealed class ResponsesContentReviewModel : IContentReviewModel
{
    private static readonly JsonSerializerOptions OutputSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };

    private readonly ContentReviewModelOptions _options;
    private readonly ILogger<ResponsesContentReviewModel> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly ResponsesClient? _client;

    /// <param name="httpClient">Replaces the network transport; used by tests with a fake HTTP handler.</param>
    public ResponsesContentReviewModel(
        ContentReviewModelOptions options,
        ILogger<ResponsesContentReviewModel> logger,
        TimeProvider timeProvider,
        HttpClient? httpClient = null)
    {
        _options = options;
        _logger = logger;
        _timeProvider = timeProvider;

        if (!options.IsConfigured)
        {
            return;
        }

        var clientOptions = new ResponsesClientOptions
        {
            Endpoint = new Uri(options.Endpoint),
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0)
        };

        if (httpClient is not null)
        {
            clientOptions.Transport = new HttpClientPipelineTransport(httpClient);
        }

        _client = new ResponsesClient(new ApiKeyCredential(options.ApiKey!), clientOptions);
    }

    public async Task<ContentReviewModelOutcome> ReviewAsync(
        ContentReviewInput input,
        CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            _logger.LogWarning("Content review model is not configured; the request is not sent.");
            return new ContentReviewModelOutcome.ProviderUnavailable();
        }

        var startedAt = _timeProvider.GetTimestamp();
        ResponseResult response;

        try
        {
            response = await _client.CreateResponseAsync(CreateRequest(input), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is ClientResultException or HttpRequestException or OperationCanceledException)
        {
            var status = (exception as ClientResultException)?.Status;
            _logger.LogWarning(
                "Content review model call failed: provider status {ProviderStatus}, after {ElapsedMs} ms.",
                status,
                ElapsedMilliseconds(startedAt));
            return new ContentReviewModelOutcome.ProviderUnavailable();
        }

        var outcome = ToOutcome(response);

        _logger.LogInformation(
            "Content review model call finished: {Outcome} in {ElapsedMs} ms, model {Model}, "
                + "input tokens {InputTokens}, output tokens {OutputTokens}, response {ResponseId}.",
            outcome.GetType().Name,
            ElapsedMilliseconds(startedAt),
            response.Model,
            response.Usage?.InputTokenCount,
            response.Usage?.OutputTokenCount,
            response.Id);

        return outcome;
    }

    private CreateResponseOptions CreateRequest(ContentReviewInput input)
    {
        var request = new CreateResponseOptions
        {
            Model = _options.Model,
            Instructions = ContentReviewPrompt.Instructions,
            StoredOutputEnabled = false,
            MaxOutputTokenCount = _options.MaxOutputTokenCount,
            TextOptions = new ResponseTextOptions
            {
                TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: ContentReviewSchema.Name,
                    jsonSchema: ContentReviewSchema.Json,
                    jsonSchemaFormatDescription: null,
                    jsonSchemaIsStrict: true)
            }
        };

        request.InputItems.Add(
            ResponseItem.CreateUserMessageItem(ContentReviewPrompt.CreateUserMessage(input.Title, input.Body)));

        return request;
    }

    private ContentReviewModelOutcome ToOutcome(ResponseResult response)
    {
        if (response.Status != ResponseStatus.Completed)
        {
            return new ContentReviewModelOutcome.InvalidOutput();
        }

        // Reasoning items are allowed; anything else besides one message (for example a tool call) is not.
        var messages = new List<MessageResponseItem>();

        foreach (var item in response.OutputItems)
        {
            switch (item)
            {
                case MessageResponseItem message:
                    messages.Add(message);
                    break;
                case ReasoningResponseItem:
                    break;
                default:
                    return new ContentReviewModelOutcome.UnexpectedOutputForm();
            }
        }

        if (messages.Count != 1)
        {
            return new ContentReviewModelOutcome.UnexpectedOutputForm();
        }

        var parts = messages[0].Content;

        if (parts.Any(part => part.Kind == ResponseContentPartKind.Refusal))
        {
            return new ContentReviewModelOutcome.Refused();
        }

        if (parts.Count != 1 || parts[0].Kind != ResponseContentPartKind.OutputText)
        {
            return new ContentReviewModelOutcome.UnexpectedOutputForm();
        }

        return Parse(parts[0].Text, response.Model);
    }

    private ContentReviewModelOutcome Parse(string? json, string? modelName)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(modelName))
        {
            return new ContentReviewModelOutcome.InvalidOutput();
        }

        ModelOutput? output;

        try
        {
            output = JsonSerializer.Deserialize<ModelOutput>(json, OutputSerializerOptions);
        }
        catch (JsonException)
        {
            return new ContentReviewModelOutcome.InvalidOutput();
        }

        if (output is null)
        {
            return new ContentReviewModelOutcome.InvalidOutput();
        }

        return new ContentReviewModelOutcome.Candidate(new ContentReviewCandidate(
            output.Outcome,
            output.Issues
                .Select(issue => new CandidateIssue(
                    issue.Category,
                    issue.Severity,
                    issue.Location,
                    issue.Excerpt,
                    issue.Explanation,
                    issue.Suggestion))
                .ToList(),
            output.MissingInformation.Select(item => item.Description).ToList(),
            modelName,
            ContentReviewPrompt.Version,
            ContentReviewSchema.Name));
    }

    private long ElapsedMilliseconds(long startedAt) =>
        (long)_timeProvider.GetElapsedTime(startedAt).TotalMilliseconds;

    /// <summary>The wire shape of content_review_v1. Every property is required, as in the schema.</summary>
    private sealed record ModelOutput(
        string Outcome,
        IReadOnlyList<ModelIssue> Issues,
        IReadOnlyList<ModelMissingInformation> MissingInformation);

    private sealed record ModelIssue(
        string Category,
        string Severity,
        string Location,
        string Excerpt,
        string Explanation,
        string? Suggestion);

    private sealed record ModelMissingInformation(string Description);
}
