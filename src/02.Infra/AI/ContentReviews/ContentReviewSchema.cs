namespace BlogSpecLab.Infra.AI.ContentReviews;

/// <summary>
/// The embedded copy of specs/002-ai-content-review/contracts/model-output.schema.json, which stays the source of
/// truth; a test keeps the two equal.
/// </summary>
public static class ContentReviewSchema
{
    public const string Name = "content_review_v1";

    private const string ResourceName = "BlogSpecLab.Infra.AI.ContentReviews.content_review_v1.schema.json";

    private static readonly Lazy<BinaryData> Content = new(Load);

    public static BinaryData Json => Content.Value;

    private static BinaryData Load()
    {
        using var stream = typeof(ContentReviewSchema).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' was not found.");

        return BinaryData.FromStream(stream);
    }
}
