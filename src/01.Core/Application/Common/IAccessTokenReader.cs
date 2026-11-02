namespace BlogSpecLab.Core.Application.Common;

public interface IAccessTokenReader
{
    /// <summary>
    /// Returns the user id for a raw token that exists, has not expired and belongs to an active user;
    /// otherwise <see langword="null"/>.
    /// </summary>
    Task<Guid?> GetActiveUserIdAsync(string rawToken, CancellationToken cancellationToken = default);
}
