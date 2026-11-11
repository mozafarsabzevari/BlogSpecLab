using System.Text;
using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Domain.ContentReviews;

namespace BlogSpecLab.Core.Application.ContentReviews;

/// <summary>
/// Checks untrusted model output against the snapshot it was produced for (research.md, R-07). Only output that
/// passes becomes a <see cref="ContentReview"/>.
/// </summary>
public sealed class ContentReviewValidator
{
    private const string Completed = "completed";
    private const string NeedsInput = "needs_input";

    private static readonly Dictionary<string, ContentIssueCategory> Categories = new(StringComparer.Ordinal)
    {
        ["spelling_and_writing"] = ContentIssueCategory.SpellingAndWriting,
        ["grammar"] = ContentIssueCategory.Grammar,
        ["clarity"] = ContentIssueCategory.Clarity,
        ["structure_and_coherence"] = ContentIssueCategory.StructureAndCoherence,
        ["title_body_mismatch"] = ContentIssueCategory.TitleBodyMismatch,
        ["claim_needs_source"] = ContentIssueCategory.ClaimNeedsSource
    };

    private static readonly Dictionary<string, ContentIssueSeverity> Severities = new(StringComparer.Ordinal)
    {
        ["high"] = ContentIssueSeverity.High,
        ["medium"] = ContentIssueSeverity.Medium,
        ["low"] = ContentIssueSeverity.Low
    };

    private static readonly Dictionary<string, ContentLocation> Locations = new(StringComparer.Ordinal)
    {
        ["title"] = ContentLocation.Title,
        ["body"] = ContentLocation.Body
    };

    public Result<ContentReview> Validate(
        ContentReviewCandidate candidate,
        ContentReviewSnapshot snapshot,
        DateTimeOffset now)
    {
        // Rules 1 to 3: allowed outcome and an outcome consistent with the lists.
        var isCompleted = candidate.Outcome == Completed;

        if (!isCompleted && candidate.Outcome != NeedsInput)
        {
            return Invalid();
        }

        if (isCompleted ? candidate.MissingInformation.Count > 0
                        : candidate.Issues.Count > 0 || candidate.MissingInformation.Count == 0)
        {
            return Invalid();
        }

        if (candidate.Issues.Count > ContentReview.MaxIssues
            || candidate.MissingInformation.Count > ContentReview.MaxMissingInformation)
        {
            return Invalid();
        }

        try
        {
            var issues = new List<ContentIssue>();

            foreach (var candidateIssue in candidate.Issues)
            {
                // Rules 1 and 4: allowed values and text limits.
                if (!Categories.TryGetValue(candidateIssue.Category, out var category)
                    || !Severities.TryGetValue(candidateIssue.Severity, out var severity)
                    || !Locations.TryGetValue(candidateIssue.Location, out var location))
                {
                    return Invalid();
                }

                var issue = new ContentIssue(
                    category,
                    severity,
                    location,
                    candidateIssue.Excerpt,
                    candidateIssue.Explanation,
                    candidateIssue.Suggestion);

                // Rule 5: an issue whose excerpt is not in the post is dropped (FR-008).
                if (AppearsIn(issue.Excerpt, location == ContentLocation.Title ? snapshot.Title : snapshot.Body))
                {
                    issues.Add(issue);
                }
            }

            // Rule 6: dropping every issue must not turn a failure into "no issues found".
            if (candidate.Issues.Count > 0 && issues.Count == 0)
            {
                return Invalid();
            }

            var missingInformation = candidate.MissingInformation
                .Select(description => new MissingInformationItem(description))
                .ToList();

            var outcome = !isCompleted
                ? ContentReviewOutcome.NeedsMoreInformation
                : issues.Count > 0 ? ContentReviewOutcome.IssuesFound : ContentReviewOutcome.NoIssuesFound;

            // Rule 7 (ordering by severity) is applied by ContentReview.Create.
            return Result.Success(ContentReview.Create(
                snapshot.PostId,
                snapshot.AuthorId,
                snapshot.Version,
                snapshot.LastModifiedAt,
                outcome,
                issues,
                missingInformation,
                candidate.ModelName,
                candidate.PromptVersion,
                candidate.SchemaVersion,
                now));
        }
        catch (ContentReviewDomainException)
        {
            return Invalid();
        }
    }

    private static bool AppearsIn(string excerpt, string text) =>
        text.Normalize(NormalizationForm.FormC)
            .Contains(excerpt.Normalize(NormalizationForm.FormC), StringComparison.Ordinal);

    private static Result<ContentReview> Invalid() => Result.Failure<ContentReview>(ContentReviewErrors.OutputInvalid());
}
