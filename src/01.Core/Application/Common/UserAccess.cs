namespace BlogSpecLab.Core.Application.Common;

public enum Role
{
    Author,
    Editor
}

public sealed record UserAccess(Guid UserId, string DisplayName, bool IsActive, IReadOnlySet<Role> Roles)
{
    public bool HasRole(Role role) => IsActive && Roles.Contains(role);
}

public interface IUserAccessReader
{
    Task<UserAccess?> GetAsync(Guid userId, CancellationToken cancellationToken = default);
}
