using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Core.Application.Posts;
using BlogSpecLab.Core.Domain.Posts;

namespace BlogSpecLab.Endpoints.Api.Posts;

public static class EditorEndpoints
{
    private const string BasePath = "/api/editor/posts";

    public static IEndpointRouteBuilder MapEditorEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(BasePath).RequireAuthorization();

        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapGet("/{id:guid}/history", GetHistoryAsync);
        group.MapPost("/{id:guid}/approve", ApproveAsync);
        group.MapPost("/{id:guid}/return", ReturnAsync);

        return routes;
    }

    private static async Task<IResult> ListAsync(
        string? status,
        EditorPostService service,
        CancellationToken cancellationToken)
    {
        if (!TryParseVisibleStatus(status, out var parsed))
        {
            return ProblemDetailsMapping.InvalidStatus();
        }

        var result = await service.ListAsync(parsed, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        EditorPostService service,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        return PostResult(await service.GetAsync(id, cancellationToken), response);
    }

    private static async Task<IResult> GetHistoryAsync(
        Guid id,
        EditorPostService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetHistoryAsync(id, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> ApproveAsync(
        Guid id,
        HttpRequest request,
        EditorPostService service,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        if (!ETagHeaders.TryReadIfMatch(request, out var expectedVersion, out var problem))
        {
            return problem;
        }

        return PostResult(await service.ApproveAsync(id, expectedVersion, cancellationToken), response);
    }

    private static async Task<IResult> ReturnAsync(
        Guid id,
        ReturnRequest? body,
        HttpRequest request,
        EditorPostService service,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        if (!ETagHeaders.TryReadIfMatch(request, out var expectedVersion, out var problem))
        {
            return problem;
        }

        return PostResult(
            await service.ReturnAsync(id, expectedVersion, body?.Reason, cancellationToken),
            response);
    }

    private static bool TryParseVisibleStatus(string? value, out PostStatus status)
    {
        status = default;

        return Enum.GetNames<PostStatus>().Contains(value)
            && Enum.TryParse(value, out status)
            && EditorPostService.VisibleStatuses.Contains(status);
    }

    private static IResult PostResult(Result<EditorPostDetail> result, HttpResponse response)
    {
        if (!result.IsSuccess)
        {
            return result.Error.ToProblem();
        }

        ETagHeaders.SetETag(response, result.Value!.Version);
        return Results.Ok(result.Value);
    }

    public sealed record ReturnRequest(string? Reason);
}
