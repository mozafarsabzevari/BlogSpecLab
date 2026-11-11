namespace BlogSpecLab.Core.Domain.ContentReviews;

/// <summary>
/// One stored AI review of a post version. It never changes after creation and never touches the post (FR-009).
/// </summary>
public sealed class ContentReview
{
    public const int MaxIssues = 50;
    public const int MaxMissingInformation = 10;
    public const int MaxMetadataLength = 100;

    private readonly List<ContentIssue> _issues = [];
    private readonly List<MissingInformationItem> _missingInformation = [];

    private ContentReview()
    {
    }

    public Guid Id { get; private set; }

    public Guid PostId { get; private set; }

    public Guid AuthorId { get; private set; }

    public Guid ReviewedPostVersion { get; private set; }

    public DateTimeOffset ReviewedPostModifiedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public ContentReviewOutcome Outcome { get; private set; }

    /// <summary>Ordered from most to least severe, keeping the original order within a severity.</summary>
    public IReadOnlyList<ContentIssue> Issues => _issues;

    public IReadOnlyList<MissingInformationItem> MissingInformation => _missingInformation;

    public string ModelName { get; private set; } = string.Empty;

    public string PromptVersion { get; private set; } = string.Empty;

    public string SchemaVersion { get; private set; } = string.Empty;

    public static ContentReview Create(
        Guid postId,
        Guid authorId,
        Guid reviewedPostVersion,
        DateTimeOffset reviewedPostModifiedAt,
        ContentReviewOutcome outcome,
        IEnumerable<ContentIssue> issues,
        IEnumerable<MissingInformationItem> missingInformation,
        string modelName,
        string promptVersion,
        string schemaVersion,
        DateTimeOffset now)
    {
        var issueList = issues.ToList();
        var missingList = missingInformation.ToList();

        EnsureConsistent(outcome, issueList.Count, missingList.Count);
        EnsureMetadata(modelName, promptVersion, schemaVersion);

        var review = new ContentReview
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            AuthorId = authorId,
            ReviewedPostVersion = reviewedPostVersion,
            ReviewedPostModifiedAt = reviewedPostModifiedAt,
            CreatedAt = now,
            Outcome = outcome,
            ModelName = modelName,
            PromptVersion = promptVersion,
            SchemaVersion = schemaVersion
        };

        review._issues.AddRange(issueList.OrderBy(issue => issue.Severity));
        review._missingInformation.AddRange(missingList);

        return review;
    }

    private static void EnsureConsistent(ContentReviewOutcome outcome, int issueCount, int missingCount)
    {
        if (issueCount > MaxIssues || missingCount > MaxMissingInformation)
        {
            throw Inconsistent(
                $"نتیجه حداکثر {MaxIssues} مشکل و {MaxMissingInformation} مورد اطلاعات ناکافی دارد.");
        }

        var consistent = outcome switch
        {
            ContentReviewOutcome.IssuesFound => issueCount > 0 && missingCount == 0,
            ContentReviewOutcome.NoIssuesFound => issueCount == 0 && missingCount == 0,
            ContentReviewOutcome.NeedsMoreInformation => issueCount == 0 && missingCount > 0,
            _ => false
        };

        if (!consistent)
        {
            throw Inconsistent("وضعیت نتیجه با فهرست مشکلات و موارد اطلاعات ناکافی سازگار نیست.");
        }
    }

    private static void EnsureMetadata(params string[] values)
    {
        if (values.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > MaxMetadataLength))
        {
            throw new ContentReviewDomainException(
                ContentReviewErrorCode.InvalidMetadata,
                "نام Model، نسخهٔ Prompt و نسخهٔ Schema الزامی‌اند.");
        }
    }

    private static ContentReviewDomainException Inconsistent(string message) =>
        new(ContentReviewErrorCode.InconsistentOutcome, message);
}
