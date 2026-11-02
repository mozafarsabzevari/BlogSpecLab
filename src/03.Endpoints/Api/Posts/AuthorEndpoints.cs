using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Posts;

namespace BlogSpecLab.Endpoints.Api.Posts;

public static class AuthorEndpoints
{
    private const string BasePath = "/api/author/posts";

    public static IEndpointRouteBuilder MapAuthorEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(BasePath).RequireAuthorization();

        group.MapPost("/", CreateAsync);
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPut("/{id:guid}", EditAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);

        return routes;
    }

    private static async Task<IResult> CreateAsync(
        PostContentRequest request,
        AuthorPostService service,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request.Title, request.Body, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error.ToProblem();
        }

        ETagHeaders.SetETag(response, result.Value!.Version);
        return Results.Created($"{BasePath}/{result.Value.Id}", result.Value);
    }

    private static async Task<IResult> ListAsync(AuthorPostService service, CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        AuthorPostService service,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);

        return PostResult(result, response);
    }

    private static async Task<IResult> EditAsync(
        Guid id,
        PostContentRequest request,
        AuthorPostService service,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var result = await service.EditAsync(id, request.Title, request.Body, cancellationToken);

        return PostResult(result, response);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        HttpRequest request,
        AuthorPostService service,
        CancellationToken cancellationToken)
    {
        if (!ETagHeaders.TryReadIfMatch(request, out var expectedVersion, out var problem))
        {
            return problem;
        }

        var result = await service.DeleteAsync(id, expectedVersion, cancellationToken);

        return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
    }

    private static IResult PostResult(Result<AuthorPostDetail> result, HttpResponse response)
    {
        if (!result.IsSuccess)
        {
            return result.Error.ToProblem();
        }

        ETagHeaders.SetETag(response, result.Value!.Version);
        return Results.Ok(result.Value);
    }

    public sealed record PostContentRequest(string? Title, string? Body);
}
