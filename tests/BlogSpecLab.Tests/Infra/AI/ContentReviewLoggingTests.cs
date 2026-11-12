using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using BlogSpecLab.Core.Application.ContentReviews;
using BlogSpecLab.Infra.AI.ContentReviews;
using BlogSpecLab.Tests.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using static BlogSpecLab.Tests.Api.Infrastructure.ContentReviewApi;

namespace BlogSpecLab.Tests.Infra.AI;

/// <summary>
/// No title, body, prompt, model output or API key reaches a log; only the metadata allowed by research.md R-05
/// (post ID, outcome, duration, model name, token counts and provider response ID).
/// </summary>
public class ContentReviewLoggingTests
{
    private const string FakeKey = "test-key-not-a-secret-7f3a";
    private const string Title = "عنوان محرمانهٔ آزمون";
    private const string Body = "متن محرمانهٔ آزمون که نباید در Log دیده شود.";
    private const string ModelText = "توضیح محرمانهٔ مدل";

    private static readonly string[] AllowedModelLogFields =
        ["Outcome", "ElapsedMs", "Model", "InputTokens", "OutputTokens", "ResponseId", "ProviderStatus", "{OriginalFormat}"];

    private readonly StubProviderHandler _handler = new();
    private readonly CapturingLoggerProvider _logs = new();

    public static TheoryData<string> ProviderAnswers() =>
        ["issues", "refusal", "invalid-json", "provider-error", "transport-error", "unexpected-form"];

    [Theory]
    [MemberData(nameof(ProviderAnswers))]
    public async Task ModelAdapter_LogsOnlyAllowedMetadata(string answer)
    {
        ProgramProvider(answer);
        var model = new ResponsesContentReviewModel(
            new ContentReviewModelOptions { ApiKey = FakeKey },
            _logs.CreateLogger<ResponsesContentReviewModel>(),
            TimeProvider.System,
            new HttpClient(_handler));

        await model.ReviewAsync(new ContentReviewInput(Title, Body));

        Assert.NotEmpty(_logs.Entries);
        AssertNoSensitiveText();
        Assert.All(
            _logs.Entries.SelectMany(entry => entry.Fields.Keys),
            field => Assert.Contains(field, AllowedModelLogFields));
    }

    [Fact]
    public async Task ModelAdapter_WithoutKey_LogsNoContent()
    {
        var model = new ResponsesContentReviewModel(
            new ContentReviewModelOptions(),
            _logs.CreateLogger<ResponsesContentReviewModel>(),
            TimeProvider.System,
            new HttpClient(_handler));

        await model.ReviewAsync(new ContentReviewInput(Title, Body));

        Assert.Empty(_handler.Requests);
        AssertNoSensitiveText();
    }

    /// <summary>The whole request path through the API, including framework and data access logs.</summary>
    [Fact]
    public async Task ApiRequestPath_LogsNoContent()
    {
        using var factory = new BlogSpecLabApiFactory();
        using var loggingFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Trace);
                logging.AddProvider(_logs);
            }));
        var users = await TestUsers.CreateAsync(factory);
        var author = loggingFactory.CreateClient();
        author.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", users.AuthorA.Token);
        var (id, _) = await CreatePostAsync(author, Title, Body);
        factory.ContentReviewModel.Returns(FakeContentReviewModel.Completed(
            FakeContentReviewModel.Issue("محرمانهٔ آزمون", explanation: ModelText)));
        _logs.Clear();

        await RequestReviewSucceedsAsync(author, id);
        factory.ContentReviewModel.Returns(new ContentReviewModelOutcome.Refused());
        await RequestReviewAsync(author, id);
        await ListReviewsAsync(author, id);

        AssertNoSensitiveText();
        Assert.Contains(_logs.Entries, entry =>
            entry.Category == typeof(ContentReviewService).FullName
            && entry.Fields.TryGetValue("PostId", out var postId) && Equals(postId, id));
    }

    private void ProgramProvider(string answer)
    {
        var issues = $$"""[{"category":"clarity","severity":"high","location":"body","excerpt":"{{Body}}","explanation":"{{ModelText}}","suggestion":null}]""";

        switch (answer)
        {
            case "issues":
                _handler.RespondWith(HttpStatusCode.OK, StubProviderHandler.Response(
                    StubProviderHandler.MessageOutput(StubProviderHandler.OutputText(StubProviderHandler.ValidOutputJson(issues: issues)))));
                break;
            case "refusal":
                _handler.RespondWith(HttpStatusCode.OK, StubProviderHandler.Response(
                    StubProviderHandler.MessageOutput(StubProviderHandler.Refusal(ModelText))));
                break;
            case "invalid-json":
                _handler.RespondWith(HttpStatusCode.OK, StubProviderHandler.Response(
                    StubProviderHandler.MessageOutput(StubProviderHandler.OutputText($"{ModelText} {Body}"))));
                break;
            case "provider-error":
                _handler.RespondWith(HttpStatusCode.InternalServerError, StubProviderHandler.ErrorBody($"{ModelText} {Body} {FakeKey}"));
                break;
            case "transport-error":
                _handler.Throw(new HttpRequestException($"{ModelText} {Body}"));
                break;
            case "unexpected-form":
                _handler.RespondWith(HttpStatusCode.OK, StubProviderHandler.Response(StubProviderHandler.FunctionCallOutput()));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(answer), answer, null);
        }
    }

    private void AssertNoSensitiveText()
    {
        string[] forbidden = [Title, Body, ModelText, FakeKey, ContentReviewPrompt.Instructions[..40]];

        foreach (var entry in _logs.Entries)
        {
            var text = entry.AllText;

            foreach (var value in forbidden)
            {
                Assert.False(
                    text.Contains(value, StringComparison.Ordinal),
                    $"Log entry from '{entry.Category}' contains forbidden text.");
            }
        }
    }
}

internal sealed record CapturedLogEntry(
    string Category,
    LogLevel Level,
    string Message,
    IReadOnlyDictionary<string, object?> Fields,
    Exception? Exception)
{
    public string AllText =>
        string.Join(
            "\n",
            Fields.Values
                .Select(value => value?.ToString() ?? string.Empty)
                .Prepend(Message)
                .Append(Exception?.ToString() ?? string.Empty));
}

/// <summary>Keeps every log entry in memory, including structured fields and exceptions.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLogEntry> _entries = new();

    public IReadOnlyList<CapturedLogEntry> Entries => _entries.ToList();

    public void Clear() => _entries.Clear();

    public ILogger<T> CreateLogger<T>() => new Logger<T>(new SingleProviderFactory(this));

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var fields = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<string, object?>();

            entries.Enqueue(new CapturedLogEntry(category, logLevel, formatter(state, exception), fields, exception));
        }
    }

    private sealed class SingleProviderFactory(ILoggerProvider provider) : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => provider.CreateLogger(categoryName);

        public void AddProvider(ILoggerProvider loggerProvider)
        {
        }

        public void Dispose()
        {
        }
    }
}
