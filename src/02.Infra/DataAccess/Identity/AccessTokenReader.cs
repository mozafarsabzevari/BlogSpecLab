using System.Security.Cryptography;
using System.Text;
using BlogSpecLab.Core.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace BlogSpecLab.Infra.DataAccess.Identity;

internal sealed class AccessTokenReader(BlogSpecLabDbContext dbContext, TimeProvider timeProvider) : IAccessTokenReader
{
    public async Task<Guid?> GetActiveUserIdAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var tokenHash = ComputeHash(rawToken);
        var token = await dbContext.AccessTokens
            .AsNoTracking()
            .Where(token => token.TokenHash == tokenHash)
            .Select(token => new { token.UserId, token.ExpiresAt, token.User!.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        if (token is null || !token.IsActive)
        {
            return null;
        }

        // SQLite cannot compare DateTimeOffset values in SQL, so expiry is checked after loading.
        if (token.ExpiresAt is { } expiresAt && expiresAt <= timeProvider.GetUtcNow())
        {
            return null;
        }

        return token.UserId;
    }

    internal static string ComputeHash(string rawToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
