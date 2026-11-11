using BlogSpecLab.Core.Application.ContentReviews;
using Xunit.Abstractions;

namespace BlogSpecLab.Tests.Evaluation;

/// <summary>
/// One call to the real provider. Checks only that the response has the expected shape and can be processed,
/// not its quality (research.md, R-12). Run with <c>--filter "Category=ProviderSmoke"</c>.
/// </summary>
[Trait("Category", "ProviderSmoke")]
public class ContentReviewProviderSmokeTests(ITestOutputHelper output)
{
    private const string Title = "نکته‌هایی برای نوشتن ایمیل کاری";
    private const string Body = "ایمیل کاری باید کوتاه و روشن باشد. در خط اول هدف خود را بنویسید تا خواننده بداند از او چه میخواهید.";

    [ProviderFact]
    public async Task RealProvider_ReturnsProcessableReview()
    {
        var model = ProviderTestEnvironment.CreateModel();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(ProviderTestEnvironment.TimeoutSeconds));

        var outcome = await model.ReviewAsync(new ContentReviewInput(Title, Body), deadline.Token);

        var candidate = Assert.IsType<ContentReviewModelOutcome.Candidate>(outcome).Review;
        Assert.Contains(candidate.Outcome, new[] { "completed", "needs_input" });
        Assert.False(string.IsNullOrWhiteSpace(candidate.ModelName));

        var now = TimeProvider.System.GetUtcNow();
        var validated = new ContentReviewValidator().Validate(
            candidate,
            new ContentReviewSnapshot(Guid.NewGuid(), Guid.NewGuid(), Title, Body, Guid.NewGuid(), now),
            now);

        // Recorded for validation.md; the post text and the key are never written out.
        output.WriteLine($"Requested model: {ProviderTestEnvironment.Options.Model}");
        output.WriteLine($"Returned model: {candidate.ModelName}");
        output.WriteLine($"Outcome: {candidate.Outcome}, issues: {candidate.Issues.Count}, missing information: {candidate.MissingInformation.Count}");
        output.WriteLine($"Validation: {(validated.IsSuccess ? "valid" : validated.Error.Code.ToString())}");
    }
}
