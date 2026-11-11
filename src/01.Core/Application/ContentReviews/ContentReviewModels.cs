using BlogSpecLab.Core.Domain.ContentReviews;

namespace BlogSpecLab.Core.Application.ContentReviews;

/// <summary>
/// The response model from contracts/http-api.md. Model name and prompt and schema versions are stored for
/// evaluation only and are not returned.
/// </summary>
public sealed record ContentReviewDetail(
    Guid Id,
    Guid PostId,
    ContentReviewOutcome Outcome,
    Guid ReviewedVersion,
    DateTimeOffset ReviewedPostModifiedAt,
    bool IsCurrentVersion,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ContentIssueDetail> Issues,
    IReadOnlyList<MissingInformationDetail> MissingInformation)
{
    internal static ContentReviewDetail From(ContentReview review, Guid currentPostVersion) =>
        new(
            review.Id,
            review.PostId,
            review.Outcome,
            review.ReviewedPostVersion,
            review.ReviewedPostModifiedAt,
            review.ReviewedPostVersion == currentPostVersion,
            review.CreatedAt,
            review.Issues
                .Select(issue => new ContentIssueDetail(
                    issue.Category,
                    issue.Severity,
                    issue.Location,
                    issue.Excerpt,
                    issue.Explanation,
                    issue.Suggestion))
                .ToList(),
            review.MissingInformation.Select(item => new MissingInformationDetail(item.Description)).ToList());
}

public sealed record ContentIssueDetail(
    ContentIssueCategory Category,
    ContentIssueSeverity Severity,
    ContentLocation Location,
    string Excerpt,
    string Explanation,
    string? Suggestion);

public sealed record MissingInformationDetail(string Description);

/// <summary>The stored post content and version that a review request was made for.</summary>
public sealed record ContentReviewSnapshot(
    Guid PostId,
    Guid AuthorId,
    string Title,
    string Body,
    Guid Version,
    DateTimeOffset LastModifiedAt);
