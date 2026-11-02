using BlogSpecLab.Infra.DataAccess.Identity;
using Microsoft.EntityFrameworkCore;

namespace BlogSpecLab.Infra.DataAccess;

public sealed class BlogSpecLabDbContext(DbContextOptions<BlogSpecLabDbContext> options) : DbContext(options)
{
    public DbSet<UserRecord> Users => Set<UserRecord>();

    public DbSet<UserRoleRecord> UserRoles => Set<UserRoleRecord>();

    public DbSet<AccessTokenRecord> AccessTokens => Set<AccessTokenRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserRecordConfiguration());
        modelBuilder.ApplyConfiguration(new UserRoleRecordConfiguration());
        modelBuilder.ApplyConfiguration(new AccessTokenRecordConfiguration());
    }
}
