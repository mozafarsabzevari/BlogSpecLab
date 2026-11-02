using BlogSpecLab.Core.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace BlogSpecLab.Infra.DataAccess.Identity;

internal sealed class UserAccessReader(BlogSpecLabDbContext dbContext) : IUserAccessReader
{
    public async Task<UserAccess?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new
            {
                user.Id,
                user.DisplayName,
                user.IsActive,
                Roles = user.Roles.Select(role => role.Role).ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        return user is null
            ? null
            : new UserAccess(user.Id, user.DisplayName, user.IsActive, user.Roles.ToHashSet());
    }
}
