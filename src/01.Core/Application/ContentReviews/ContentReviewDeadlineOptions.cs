namespace BlogSpecLab.Core.Application.ContentReviews;

/// <summary>Read from the <c>ContentReview</c> configuration section (research.md, R-09).</summary>
public sealed class ContentReviewDeadlineOptions
{
    public const int DefaultTimeoutSeconds = 60;
    public const int MinTimeoutSeconds = 1;
    public const int MaxTimeoutSeconds = 60;

    public int TimeoutSeconds { get; init; } = DefaultTimeoutSeconds;

    public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds);

    /// <summary>Called at startup so that a wrong value stops the application instead of a later request.</summary>
    /// <exception cref="InvalidOperationException">The value is outside the allowed range.</exception>
    public void Validate()
    {
        if (TimeoutSeconds is < MinTimeoutSeconds or > MaxTimeoutSeconds)
        {
            throw new InvalidOperationException(
                $"ContentReview:TimeoutSeconds must be between {MinTimeoutSeconds} and {MaxTimeoutSeconds}.");
        }
    }
}
