using System.Buffers.Text;
using System.Security.Cryptography;
using BlogSpecLab.Core.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace BlogSpecLab.Infra.DataAccess.Identity;

public sealed record ProvisionedUser(Guid UserId, string Token);

/// <summary>
/// Creates users and issues tokens for local setup and tests. The raw token is returned once and never stored.
/// </summary>
public sealed class LocalProvisioner(BlogSpecLabDbContext dbContext, TimeProvider timeProvider)
{
    private const int TokenSizeInBytes = 32;

    public async Task<ProvisionedUser> ProvisionUserAsync(
        string displayName,
        IReadOnlyCollection<Role> roles,
        TimeSpan? tokenLifetime = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(roles);
        EnsureValidLifetime(tokenLifetime);

        var now = timeProvider.GetUtcNow();
        var user = new UserRecord
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName.Trim(),
            IsActive = true,
            CreatedAt = now,
            Roles = roles.Distinct().Select(role => new UserRoleRecord { Role = role }).ToList()
        };

        dbContext.Users.Add(user);
        var rawToken = AddToken(user.Id, now, tokenLifetime);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ProvisionedUser(user.Id, rawToken);
    }

    /// <returns>The raw token, or <see langword="null"/> when the user does not exist.</returns>
    public async Task<string?> IssueTokenAsync(
        Guid userId,
        TimeSpan? tokenLifetime = null,
        CancellationToken cancellationToken = default)
    {
        EnsureValidLifetime(tokenLifetime);

        if (!await dbContext.Users.AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return null;
        }

        var rawToken = AddToken(userId, timeProvider.GetUtcNow(), tokenLifetime);
        await dbContext.SaveChangesAsync(cancellationToken);

        return rawToken;
    }

    private string AddToken(Guid userId, DateTimeOffset now, TimeSpan? tokenLifetime)
    {
        var rawToken = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenSizeInBytes));

        dbContext.AccessTokens.Add(new AccessTokenRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = AccessTokenReader.ComputeHash(rawToken),
            CreatedAt = now,
            ExpiresAt = now + tokenLifetime
        });

        return rawToken;
    }

    private static void EnsureValidLifetime(TimeSpan? tokenLifetime)
    {
        if (tokenLifetime is { } lifetime && lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(tokenLifetime), "Token lifetime must be positive.");
        }
    }
}
