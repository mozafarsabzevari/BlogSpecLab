using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogSpecLab.Infra.DataAccess.Identity;

internal sealed class UserRecordConfiguration : IEntityTypeConfiguration<UserRecord>
{
    public void Configure(EntityTypeBuilder<UserRecord> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.DisplayName).IsRequired();

        builder.HasMany(user => user.Roles)
            .WithOne()
            .HasForeignKey(role => role.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserRoleRecordConfiguration : IEntityTypeConfiguration<UserRoleRecord>
{
    public void Configure(EntityTypeBuilder<UserRoleRecord> builder)
    {
        builder.ToTable("UserRoles");
        builder.HasKey(role => new { role.UserId, role.Role });
        builder.Property(role => role.Role)
            .HasConversion<string>()
            .HasMaxLength(20);
    }
}

internal sealed class AccessTokenRecordConfiguration : IEntityTypeConfiguration<AccessTokenRecord>
{
    public void Configure(EntityTypeBuilder<AccessTokenRecord> builder)
    {
        builder.ToTable("AccessTokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.TokenHash)
            .IsRequired()
            .HasMaxLength(64);
        builder.HasIndex(token => token.TokenHash).IsUnique();

        builder.HasOne(token => token.User)
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
