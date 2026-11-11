using BlogSpecLab.Core.Application.ContentReviews;
using BlogSpecLab.Endpoints.Api.Posts;

namespace BlogSpecLab.Endpoints.Api.ContentReviews;

public static class ContentReviewEndpoints
{
    private const string BasePath = "/api/author/posts/{postId:guid}/content-reviews";

    public static IEndpointRouteBuilder MapContentReviewEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(BasePath).RequireAuthorization();

        // No If-Match: a review never changes the post (research.md, R-15).
        group.MapPost("/", RequestAsync);
        group.MapGet("/", ListAsync);

        return routes;
    }

    private static async Task<IResult> RequestAsync(
        Guid postId,
        ContentReviewService service,
        TimeProvider timeProvider,
        ILogger<ContentReviewService> logger,
        CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetTimestamp();
        var result = await service.RequestAsync(postId, cancellationToken);

        // Only metadata is logged; never post content or model output (research.md, R-05).
        logger.LogInformation(
            "Content review for post {PostId} ended with {Result} in {ElapsedMs} ms.",
            postId,
            result.IsSuccess ? result.Value!.Outcome.ToString() : result.Error.Code.ToString(),
            (long)timeProvider.GetElapsedTime(startedAt).TotalMilliseconds);

        return result.IsSuccess
            ? Results.Created((string?)null, result.Value)
            : result.Error.ToProblem();
    }

    private static async Task<IResult> ListAsync(
        Guid postId,
        ContentReviewService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(postId, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }
}
