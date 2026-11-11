using BlogSpecLab.Core.Application.ContentReviews;

namespace BlogSpecLab.Tests.Api.Infrastructure;

/// <summary>
/// Stands in for the real model in every ordinary test. Returns the programmed outcome, records each input, and
/// can be held until the test releases it, so that ordering is controlled without sleeping.
/// </summary>
public sealed class FakeContentReviewModel : IContentReviewModel
{
    public const string ModelName = "fake-model";
    public const string PromptVersion = "content-review-prompt-v1";
    public const string SchemaVersion = "content_review_v1";

    private readonly object _lock = new();
    private readonly List<ContentReviewInput> _inputs = [];
    private Func<ContentReviewInput, ContentReviewModelOutcome> _respond = _ => Completed();
    private TaskCompletionSource? _gate;
    private TaskCompletionSource _called = NewSignal();

    public IReadOnlyList<ContentReviewInput> Inputs
    {
        get
        {
            lock (_lock)
            {
                return _inputs.ToList();
            }
        }
    }

    public void Returns(ContentReviewModelOutcome outcome) => _respond = _ => outcome;

    public void Returns(Func<ContentReviewInput, ContentReviewModelOutcome> respond) => _respond = respond;

    /// <summary>Calls wait until <see cref="Release"/> or until their cancellation token is cancelled.</summary>
    public void Hold()
    {
        lock (_lock)
        {
            _gate = NewSignal();
            _called = NewSignal();
        }
    }

    public void Release()
    {
        lock (_lock)
        {
            _gate?.TrySetResult();
            _gate = null;
        }
    }

    /// <summary>Completes once a held call has started waiting.</summary>
    public Task WhenCalled
    {
        get
        {
            lock (_lock)
            {
                return _called.Task;
            }
        }
    }

    public async Task<ContentReviewModelOutcome> ReviewAsync(
        ContentReviewInput input,
        CancellationToken cancellationToken = default)
    {
        Task? gate;

        lock (_lock)
        {
            _inputs.Add(input);
            gate = _gate?.Task;
            _called.TrySetResult();
        }

        if (gate is not null)
        {
            await gate.WaitAsync(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return _respond(input);
    }

    public static ContentReviewModelOutcome Completed(params CandidateIssue[] issues) =>
        new ContentReviewModelOutcome.Candidate(Candidate("completed", issues, []));

    public static ContentReviewModelOutcome NeedsInput(params string[] missingInformation) =>
        new ContentReviewModelOutcome.Candidate(Candidate("needs_input", [], missingInformation));

    public static ContentReviewCandidate Candidate(
        string outcome,
        IReadOnlyList<CandidateIssue> issues,
        IReadOnlyList<string> missingInformation) =>
        new(outcome, issues, missingInformation, ModelName, PromptVersion, SchemaVersion);

    public static CandidateIssue Issue(
        string excerpt,
        string location = "body",
        string severity = "medium",
        string category = "clarity",
        string explanation = "توضیح مشکل",
        string? suggestion = null) =>
        new(category, severity, location, excerpt, explanation, suggestion);

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
