using System.Security.Claims;
using System.Text.Encodings.Web;
using BlogSpecLab.Core.Application.Common;
using BlogSpecLab.Endpoints.Api.Posts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BlogSpecLab.Endpoints.Api.Authentication;

/// <summary>
/// Identifies the caller from an opaque bearer token. Produces only the user id claim; roles are never
/// derived from the request.
/// </summary>
public sealed class OpaqueTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IAccessTokenReader accessTokenReader)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "OpaqueToken";

    private const string BearerPrefix = "Bearer ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? header = Request.Headers.Authorization;

        if (string.IsNullOrEmpty(header))
        {
            return AuthenticateResult.NoResult();
        }

        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail("Unsupported authorization scheme.");
        }

        var rawToken = header[BearerPrefix.Length..].Trim();

        if (rawToken.Length == 0)
        {
            return AuthenticateResult.Fail("Missing bearer token.");
        }

        var userId = await accessTokenReader.GetActiveUserIdAsync(rawToken, Context.RequestAborted);

        if (userId is null)
        {
            return AuthenticateResult.Fail("Invalid or expired token.");
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())],
            SchemeName);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        return ProblemDetailsMapping.Unauthenticated().ExecuteAsync(Context);
    }
}
