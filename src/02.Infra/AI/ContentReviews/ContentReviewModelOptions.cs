namespace BlogSpecLab.Infra.AI.ContentReviews;

/// <summary>
/// The <c>ContentReview</c> configuration section (research.md, R-13). <see cref="ApiKey"/> comes only from user
/// secrets or the <c>ContentReview__ApiKey</c> environment variable, never from appsettings.
/// </summary>
public sealed class ContentReviewModelOptions
{
    public const string SectionName = "ContentReview";

    public string Endpoint { get; set; } = "https://api.x.ai/v1";

    public string Model { get; set; } = "grok-4.7";

    public string? ApiKey { get; set; }

    public int MaxOutputTokenCount { get; set; } = 8000;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(Model)
        && Uri.TryCreate(Endpoint, UriKind.Absolute, out _);
}
