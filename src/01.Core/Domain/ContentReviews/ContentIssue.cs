namespace BlogSpecLab.Core.Domain.ContentReviews;

public sealed class ContentIssue
{
    public const int MaxExcerptLength = 300;
    public const int MaxTextLength = 1000;

    private ContentIssue()
    {
    }

    /// <remarks>
    /// <paramref name="excerpt"/> is kept exactly as given, because it must match the post text verbatim.
    /// </remarks>
    public ContentIssue(
        ContentIssueCategory category,
        ContentIssueSeverity severity,
        ContentLocation location,
        string excerpt,
        string explanation,
        string? suggestion)
    {
        if (!Enum.IsDefined(category) || !Enum.IsDefined(severity) || !Enum.IsDefined(location))
        {
            throw Invalid("دسته، شدت یا محل مشکل نامعتبر است.");
        }

        if (string.IsNullOrWhiteSpace(excerpt) || excerpt.Length > MaxExcerptLength)
        {
            throw Invalid($"بخش ارجاع‌شدهٔ مشکل الزامی است و حداکثر {MaxExcerptLength} نویسه دارد.");
        }

        if (string.IsNullOrWhiteSpace(explanation) || explanation.Length > MaxTextLength)
        {
            throw Invalid($"توضیح مشکل الزامی است و حداکثر {MaxTextLength} نویسه دارد.");
        }

        if (suggestion is not null && suggestion.Length > MaxTextLength)
        {
            throw Invalid($"پیشنهاد اصلاح حداکثر {MaxTextLength} نویسه دارد.");
        }

        Category = category;
        Severity = severity;
        Location = location;
        Excerpt = excerpt;
        Explanation = explanation;
        Suggestion = string.IsNullOrWhiteSpace(suggestion) ? null : suggestion;
    }

    public ContentIssueCategory Category { get; private set; }

    public ContentIssueSeverity Severity { get; private set; }

    public ContentLocation Location { get; private set; }

    public string Excerpt { get; private set; } = string.Empty;

    public string Explanation { get; private set; } = string.Empty;

    public string? Suggestion { get; private set; }

    private static ContentReviewDomainException Invalid(string message) =>
        new(ContentReviewErrorCode.InvalidIssue, message);
}
