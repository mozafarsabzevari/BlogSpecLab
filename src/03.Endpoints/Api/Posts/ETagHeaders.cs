using System.Diagnostics.CodeAnalysis;

namespace BlogSpecLab.Endpoints.Api.Posts;

/// <summary>
/// Post versions travel as strong ETags of the form <c>"guid"</c>. Wildcards, weak tags and lists are rejected.
/// </summary>
public static class ETagHeaders
{
    public static void SetETag(HttpResponse response, Guid version) =>
        response.Headers.ETag = $"\"{version}\"";

    public static bool TryReadIfMatch(
        HttpRequest request,
        out Guid version,
        [NotNullWhen(false)] out IResult? problem)
    {
        version = default;
        var values = request.Headers.IfMatch;

        if (values.Count == 0 || values.All(string.IsNullOrWhiteSpace))
        {
            problem = ProblemDetailsMapping.PreconditionRequired();
            return false;
        }

        var value = values.Count == 1 ? values[0]!.Trim() : string.Empty;

        if (value.Length < 3
            || value[0] != '"'
            || value[^1] != '"'
            || !Guid.TryParseExact(value[1..^1], "D", out version))
        {
            problem = ProblemDetailsMapping.InvalidIfMatch();
            return false;
        }

        problem = null;
        return true;
    }
}
