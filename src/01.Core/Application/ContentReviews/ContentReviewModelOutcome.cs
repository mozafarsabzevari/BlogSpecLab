namespace BlogSpecLab.Core.Application.ContentReviews;

/// <summary>Exactly one of the outcomes in contracts/application-ai.md.</summary>
public abstract record ContentReviewModelOutcome
{
    private ContentReviewModelOutcome()
    {
    }

    /// <summary>A complete output that matched the schema; not yet validated by the application.</summary>
    public sealed record Candidate(ContentReviewCandidate Review) : ContentReviewModelOutcome;

    public sealed record Refused : ContentReviewModelOutcome;

    public sealed record ProviderUnavailable : ContentReviewModelOutcome;

    public sealed record InvalidOutput : ContentReviewModelOutcome;

    public sealed record UnexpectedOutputForm : ContentReviewModelOutcome;
}

/// <summary>Model output as received. Values are untrusted until <see cref="ContentReviewValidator"/> accepts them.</summary>
public sealed record ContentReviewCandidate(
    string Outcome,
    IReadOnlyList<CandidateIssue> Issues,
    IReadOnlyList<string> MissingInformation,
    string ModelName,
    string PromptVersion,
    string SchemaVersion);

public sealed record CandidateIssue(
    string Category,
    string Severity,
    string Location,
    string Excerpt,
    string Explanation,
    string? Suggestion);
