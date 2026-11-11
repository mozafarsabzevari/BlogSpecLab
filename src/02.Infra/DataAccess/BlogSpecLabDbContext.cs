using BlogSpecLab.Core.Domain.ContentReviews;
using BlogSpecLab.Core.Domain.Posts;
using BlogSpecLab.Infra.DataAccess.ContentReviews;
using BlogSpecLab.Infra.DataAccess.Identity;
using BlogSpecLab.Infra.DataAccess.Posts;
using Microsoft.EntityFrameworkCore;

namespace BlogSpecLab.Infra.DataAccess;

public sealed class BlogSpecLabDbContext(DbContextOptions<BlogSpecLabDbContext> options) : DbContext(options)
{
    public DbSet<UserRecord> Users => Set<UserRecord>();

    public DbSet<UserRoleRecord> UserRoles => Set<UserRoleRecord>();

    public DbSet<AccessTokenRecord> AccessTokens => Set<AccessTokenRecord>();

    public DbSet<Post> Posts => Set<Post>();

    public DbSet<StatusChange> PostStatusChanges => Set<StatusChange>();

    public DbSet<ContentReview> ContentReviews => Set<ContentReview>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserRecordConfiguration());
        modelBuilder.ApplyConfiguration(new UserRoleRecordConfiguration());
        modelBuilder.ApplyConfiguration(new AccessTokenRecordConfiguration());
        modelBuilder.ApplyConfiguration(new PostConfiguration());
        modelBuilder.ApplyConfiguration(new StatusChangeConfiguration());
        modelBuilder.ApplyConfiguration(new ContentReviewConfiguration());
    }
}
