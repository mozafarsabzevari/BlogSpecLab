using BlogSpecLab.Core.Domain.Posts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BlogSpecLab.Infra.DataAccess.Posts;

internal sealed class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("Posts");
        builder.HasKey(post => post.Id);
        builder.Property(post => post.Id).ValueGeneratedNever();

        builder.Property(post => post.Title).IsRequired().HasMaxLength(Post.MaxTitleLength);
        builder.Property(post => post.Body).IsRequired().HasMaxLength(Post.MaxBodyLength);
        builder.Property(post => post.AuthorDisplayName).IsRequired();
        builder.Property(post => post.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(post => post.Version).IsConcurrencyToken();

        // SQLite cannot sort or compare DateTimeOffset values, so times are stored as sortable UTC ticks.
        builder.Property(post => post.CreatedAt).HasConversion<DateTimeOffsetToBinaryConverter>();
        builder.Property(post => post.LastModifiedAt).HasConversion<DateTimeOffsetToBinaryConverter>();
        builder.Property(post => post.LastPublishedAt).HasConversion<DateTimeOffsetToBinaryConverter>();

        builder.Ignore(post => post.HasEverBeenPublished);

        builder.HasMany(post => post.StatusChanges)
            .WithOne()
            .HasForeignKey(change => change.PostId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(post => post.StatusChanges).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(post => post.AuthorId);
        builder.HasIndex(post => new { post.Status, post.LastPublishedAt });
    }
}

internal sealed class StatusChangeConfiguration : IEntityTypeConfiguration<StatusChange>
{
    public void Configure(EntityTypeBuilder<StatusChange> builder)
    {
        builder.ToTable("PostStatusChanges");
        builder.HasKey(change => change.Id);
        builder.Property(change => change.Id).ValueGeneratedNever();

        builder.Property(change => change.ActorDisplayName).IsRequired();
        builder.Property(change => change.FromStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(change => change.ToStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(change => change.OccurredAt).HasConversion<DateTimeOffsetToBinaryConverter>();
    }
}
