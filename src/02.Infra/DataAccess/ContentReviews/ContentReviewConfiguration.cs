using BlogSpecLab.Core.Domain.ContentReviews;
using BlogSpecLab.Core.Domain.Posts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BlogSpecLab.Infra.DataAccess.ContentReviews;

internal sealed class ContentReviewConfiguration : IEntityTypeConfiguration<ContentReview>
{
    public void Configure(EntityTypeBuilder<ContentReview> builder)
    {
        builder.ToTable("ContentReviews");
        builder.HasKey(review => review.Id);
        builder.Property(review => review.Id).ValueGeneratedNever();

        builder.Property(review => review.Outcome).HasConversion<string>().HasMaxLength(30);
        builder.Property(review => review.ModelName).IsRequired().HasMaxLength(ContentReview.MaxMetadataLength);
        builder.Property(review => review.PromptVersion).IsRequired().HasMaxLength(ContentReview.MaxMetadataLength);
        builder.Property(review => review.SchemaVersion).IsRequired().HasMaxLength(ContentReview.MaxMetadataLength);

        // Same storage as post times, so that reviews can be ordered by time in SQLite.
        builder.Property(review => review.CreatedAt).HasConversion<DateTimeOffsetToBinaryConverter>();
        builder.Property(review => review.ReviewedPostModifiedAt).HasConversion<DateTimeOffsetToBinaryConverter>();

        builder.HasOne<Post>()
            .WithMany()
            .HasForeignKey(review => review.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsMany(review => review.Issues, issues =>
        {
            issues.ToJson();
            issues.Property(issue => issue.Category).HasConversion<string>();
            issues.Property(issue => issue.Severity).HasConversion<string>();
            issues.Property(issue => issue.Location).HasConversion<string>();
        });
        builder.Navigation(review => review.Issues).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(review => review.MissingInformation, missing => missing.ToJson());
        builder.Navigation(review => review.MissingInformation).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(review => new { review.PostId, review.CreatedAt });
    }
}
