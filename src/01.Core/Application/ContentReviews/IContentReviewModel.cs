namespace BlogSpecLab.Core.Application.ContentReviews;

/// <summary>
/// Asks a language model to review post content. Implemented in Infra.AI; no SDK type crosses this boundary
/// (contracts/application-ai.md).
/// </summary>
public interface IContentReviewModel
{
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled. It is never turned into an outcome, so that the caller
    /// can tell a deadline from a cancelled request.
    /// </exception>
    Task<ContentReviewModelOutcome> ReviewAsync(ContentReviewInput input, CancellationToken cancellationToken = default);
}

/// <summary>Only the title and body are sent to the model (research.md, R-05).</summary>
public sealed record ContentReviewInput(string Title, string Body);
