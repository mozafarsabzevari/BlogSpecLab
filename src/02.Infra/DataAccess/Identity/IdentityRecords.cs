using BlogSpecLab.Core.Application.Common;

namespace BlogSpecLab.Infra.DataAccess.Identity;

public sealed class UserRecord
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<UserRoleRecord> Roles { get; set; } = [];
}

public sealed class UserRoleRecord
{
    public Guid UserId { get; set; }

    public Role Role { get; set; }
}

public sealed class AccessTokenRecord
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public UserRecord? User { get; set; }
}
