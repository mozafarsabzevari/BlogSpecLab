using BlogSpecLab.Core.Application.Posts;

namespace BlogSpecLab.Endpoints.Api.Posts;

/// <summary>Reader routes: no sign-in, and no <c>ETag</c> or version in responses (R-11).</summary>
public static class PublicEndpoints
{
    private const string BasePath = "/api/posts";

    public static IEndpointRouteBuilder MapPublicEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(BasePath).AllowAnonymous();

        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);

        return routes;
    }

    private static async Task<IResult> ListAsync(PublicPostQueries queries, CancellationToken cancellationToken) =>
        Results.Ok(await queries.ListAsync(cancellationToken));

    private static async Task<IResult> GetAsync(
        Guid id,
        PublicPostQueries queries,
        CancellationToken cancellationToken)
    {
        var result = await queries.GetAsync(id, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }
}
